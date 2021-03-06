﻿using System.Collections.Generic;

namespace Composr.Core
{

    public class SearchResults
    {
        public SearchResults()
        {
            Hits = new List<Hit>();
        }

        public List<Hit> Hits { get; set; }
        //public SearchCriteria SearchCriteria { get; set; }
        public int HitsCount { get; set; }
    }


    /// <summary>
    /// data structure to represent a post in the form of a search result
    /// </summary>
    public class Hit
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public string URN { get; set; }
        public string Id { get; set; }
        public string DatePublished { get; set; }
        public string Body{ get; set; }
        public string Thumbnail { get; set; }
        public string MetaDescription { get; set; }
        public string Yield { get; set; }
        public string Tags { get; set; }
        public PostImage PostImage { get; set; }
        public Dictionary<Locale, string> Translations { get; set; }
        public int DocumentId { get; set; }
        public string StructuredDataJsonLD { get; set; }
    }

    public enum SearchSortOrder
    {
        Title,
        MostRecent,
        BestMatch
    }

    public enum SearchType
    {
        AutoComplete,
        Default,
        URN,
        MoreLikeThis
    }

    public class SearchCriteria
    {
        public SearchCriteria()
        {
            SearchSortOrder = SearchSortOrder.BestMatch;
        }

        public int BlogID { get; set; }
        public Locale? Locale { get; set; }
        public string SearchTerm { get; set; }
        public SearchSortOrder SearchSortOrder { get; set; }
        public int Limit { get; set; } = 100;
        public int Start { get; set; } = 0;
        public SearchType SearchType { get; set; } = SearchType.Default;
        public string Tags { get; set; }
        public int DocumentId { get; set; }
    }
}