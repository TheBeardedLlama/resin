﻿using System;

namespace Resin
{
    public class Tfidf
    {
        private readonly double _idf;
        private readonly string _idfTrace;
        
        public double Idf
        {
            get { return _idf; }
        }

        public Tfidf(int numDocs, int docFreq)
        {
            _idf = Math.Log10(numDocs - docFreq / (double)docFreq);
            _idfTrace = string.Format("log({0}-{1}/{1})", numDocs, docFreq);
        }

        public Tfidf(double idf)
        {
            _idf = idf;
        }

        public void Score(DocumentScore doc)
        {
            var tf = 1 + Math.Log10(Math.Sqrt(doc.TermFrequency));
            doc.Score = tf * _idf;
            doc.Scorer = this;
            doc.Trace.AppendFormat("1+log(sqrt({0}))*({1}) ", doc.TermFrequency, _idfTrace);
        }
    }
}