using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using log4net;

namespace Resin.IO
{
    public class LazyTrie
    {
        private readonly string _fileName;
        private readonly int _depth;
        private readonly char _value;
        private bool _eow;
        private readonly int _rowIndex;
        private Dictionary<char, Trie> _nodes;
        private static readonly ILog Log = LogManager.GetLogger(typeof(LazyTrie));
        private readonly TrieReader _reader;

        public char Val { get { return _value; } }
        public int Depth { get { return _depth; } }
        public bool Eow { get { return _eow; } }

        public LazyTrie(string containerId, string directory)
        {
            _reader = new TrieReader(containerId, directory);
            _nodes
        }

        public IEnumerable<LazyTrie> ResolveChildren()
        {
            if (_nodes == null)
            {
                _nodes = new Dictionary<char, Trie>();
                foreach (var node in ReadChildren())
                {
                    _nodes[node.Val] = node;
                }
            }
            return _nodes.Values;
        }

        private IEnumerable<LazyTrie> ReadChildren()
        {
            if (!File.Exists(_fileName))
            {
                yield break;
            }
            using (var fs = File.Open(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.Unicode))
            {
                Ffw(reader);
                var rowIndex = _rowIndex;
                while (true)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) break;
                    var id = line.Substring(0, line.IndexOf(':'));
                    var c = line[0];
                    var eow = int.Parse(line.Substring(id.Length + 1)) == 1;
                    var depth = Int32.Parse(id.Substring(id.IndexOf('.') + 1));
                    if (depth == _depth + 1)
                    {
                        rowIndex++;
                        var trie = new LazyTrie(c, depth, eow, rowIndex, _fileName);
                        yield return trie;
                    }
                    else if (depth == _depth)
                    {
                        break;
                    }
                }
            }
        }

        public bool TryResolveChild(char c, int depth, out LazyTrie trie)
        {

        }

        public bool ContainsToken(string token)
        {
            var timer = new Stopwatch();
            timer.Start();

            var nodes = new List<char>();
            LazyTrie child;
            if (_nodes.TryGetValue(token[0], out child))
            {
                child.ExactScan(token, nodes);
            }
            Log.DebugFormat("exact scan for {0} in {1}", token, timer.Elapsed);
            if (nodes.Count > 0)
            {
                return true;
            }
            return false;
        }

        public void ExactScan(string prefix, List<char> chars)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination.
                if (_eow)
                {
                    chars.Add(_value);
                }
            }
            else if (prefix[0] == _value)
            {
                LazyTrie child;
                if (TryResolveChild(prefix[1], _depth, out child))
                {
                    child.ExactScan(prefix.Substring(1), chars);
                }
            }
        }

        public IEnumerable<string> Similar(string word, int edits)
        {
            var words = new List<Word>();
            SimScan(word, word, edits, words);
            return words.OrderBy(w => w.Distance).Select(w => w.Value);
        }

        public void SimScan(string word, string state, int edits, IList<Word> words)
        {
            foreach (var child in ResolveChildren())
            {
                var tmp = state.ReplaceAt(child.Depth, child.Val);
                if (Levenshtein.Distance(word, tmp) <= edits)
                {
                    if (child.Eow)
                    {
                        var potential = tmp.Substring(0, child.Depth + 1);
                        var distance = Levenshtein.Distance(word, potential);
                        if (distance <= edits) words.Add(new Word { Value = potential, Distance = distance });
                    }
                    child.SimScan(word, tmp, edits, words);
                }
            }
        }

        public IEnumerable<string> Prefixed(string prefix)
        {
            var words = new List<List<char>>();
            LazyTrie child;
            if (TryResolveChild(prefix[0], 0, out child))
            {
                child.PrefixScan(new List<char>(prefix), prefix, words);
            }
            return words.Select(s => new string(s.ToArray()));
        }

        public void PrefixScan(List<char> state, string prefix, List<List<char>> words)
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("prefix");

            if (prefix.Length == 1 && prefix[0] == _value)
            {
                // The scan has reached its destination. Find words derived from this node.
                if (_eow) words.Add(state);
                foreach (var node in ResolveChildren())
                {
                    var newState = new List<char>(state.Count + 1);
                    foreach (var c in state) newState.Add(c);
                    newState.Add(node.Val);
                    node.PrefixScan(newState, new string(new[] { node.Val }), words);
                }
            }
            else if (prefix[0] == _value)
            {
                LazyTrie child;
                if (TryResolveChild(prefix[1], _depth + 1, out child))
                {
                    child.PrefixScan(state, prefix.Substring(1), words);
                }
            }
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}:{2}", _value, _depth, _eow);
        }
    }

    //public interface ITrie
    //{
    //    bool TryResolveChild(char c, int depth, out ITrie trie);
    //    IEnumerable<ITrie> ResolveChildren();
    //    char Val { get; }
    //    int Depth { get; }
    //    bool Eow { get; }
    //    bool ContainsToken(string token);
    //    void ExactScan(string prefix, List<char> chars);
    //    IEnumerable<string> Similar(string word, int edits);
    //    void SimScan(string word, string state, int edits, IList<Word> words);
    //    IEnumerable<string> Prefixed(string prefix);
    //    void PrefixScan(List<char> state, string prefix, List<List<char>> words);
    //}
}