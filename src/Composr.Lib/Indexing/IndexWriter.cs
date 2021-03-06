﻿using Composr.Core;
using Composr.Lib.StructuredData;
using Composr.Lib.Util;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Store;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Composr.Lib.Indexing
{
    public class IndexWriter : IIndexWriter
    {
        private ILogger logger;
        private Directory indexDirectory;
        private IStructuredDataTranslator structuredDataTranslator;

        public IndexWriter()
        {
            logger = Logging.CreateLogger<IndexWriter>();            
            indexDirectory = FSDirectory.Open(new System.IO.DirectoryInfo(Settings.IndexDirectory));
        }
        
        public void IndexPosts(IList<Post> posts, ISynonymEngine synonymEngine)
        {
            PerFieldAnalyzerWrapper analyzerWrapper = new PerFieldAnalyzerWrapper(new ComposrAnalyzer(Lucene.Net.Util.Version.LUCENE_30));      
            if(synonymEngine != null) analyzerWrapper.AddAnalyzer(IndexFields.Tags, new ComposrAnalyzer(Lucene.Net.Util.Version.LUCENE_30) { SynonymEngine = synonymEngine });

            structuredDataTranslator = null;
            if (posts != null && posts.Count > 0 && posts[0].Blog.Attributes.ContainsKey(BlogAttributeKeys.StructuredDataTranslator))
                structuredDataTranslator = GetStructuredDataTranslator(posts[0].Blog.Attributes[BlogAttributeKeys.StructuredDataTranslator]);

            using (var indexWriter = new Lucene.Net.Index.IndexWriter(indexDirectory, analyzerWrapper, Lucene.Net.Index.IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var post in posts)
                    indexWriter.AddDocument(CreateDocument(post));
                
                indexWriter.Commit();
                indexWriter.Optimize();
            }
        }

        private IStructuredDataTranslator GetStructuredDataTranslator(string type)
        {
            return StructuredDataTranslatorFactory.CreateTranslator(type);
        }

        public void IndexCategories(IList<Category> categories)
        {
            var analyzer = new ComposrAnalyzer(Lucene.Net.Util.Version.LUCENE_30);
            using (var indexWriter = new Lucene.Net.Index.IndexWriter(indexDirectory, analyzer, Lucene.Net.Index.IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var category in categories)
                    indexWriter.AddDocument(CreateDocument(category));

                indexWriter.Commit();
                indexWriter.Optimize();
            }
        }
        
        private Document CreateDocument(Post post)
        {
            Document doc = new Document();
            doc.Add(new Field(IndexFields.BlogID, post.Blog.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.Locale, post.Blog.Locale.ToString().ToLowerInvariant(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            //doc.Add(new Field(IndexFields.IndexedPostBody, post.Body.StripHTMLTags().StripLineFeedCarriageReturn(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field(IndexFields.PostBody, post.Body, Field.Store.YES, Field.Index.ANALYZED));

            doc.Add(new Field(IndexFields.PostDatePublished, post.DatePublished.Value.ToString("MMM d, yyyy"), Field.Store.YES, Field.Index.NO));
            doc.Add(new Field(IndexFields.PostDatePublishedTicks, post.DatePublished.Value.ToString("yyyyMMddhhmmss"), Field.Store.NO, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field(IndexFields.PostID, post.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.Title, post.Title, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field(IndexFields.URN, post.URN.TrimEnd('/'), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            if (post.Attributes.ContainsKey(PostAttributeKeys.MetaDescription) && !string.IsNullOrWhiteSpace(post.Attributes[PostAttributeKeys.MetaDescription]))
                doc.Add(new Field(IndexFields.PostMetaDescription, post.Attributes[PostAttributeKeys.MetaDescription], Field.Store.YES, Field.Index.ANALYZED));

            if (post.Attributes.ContainsKey(PostAttributeKeys.Tags) && !string.IsNullOrWhiteSpace(post.Attributes[PostAttributeKeys.Tags]))
                foreach(var tag in post.Attributes[PostAttributeKeys.Tags].Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    doc.Add(new Field(IndexFields.Tags, tag, Field.Store.YES, Field.Index.ANALYZED));

            string snippet = PrepareSnippet(post.Body);
            doc.Add(new Field(IndexFields.PostSnippet, snippet, Field.Store.YES, Field.Index.NO));
            bool hasImage = false;

            if(post.Images.Count > 0)
            {
                var img = post.Images.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(img.Url))
                {
                    hasImage = true;
                    doc.Add(new Field(IndexFields.ImageUrl, post.Blog.Attributes[BlogAttributeKeys.ImageLocation] + img.Url, Field.Store.YES, Field.Index.NO));
                    if (!string.IsNullOrWhiteSpace(img.Caption)) doc.Add(new Field(IndexFields.ImageCaption, img.Caption, Field.Store.YES, Field.Index.NO));
                }
            }

            doc.Add(new Field(IndexFields.HasImage, (hasImage? "y": "n"), Field.Store.NO, Field.Index.NOT_ANALYZED_NO_NORMS));

            if(post.Translations != null && post.Translations.Count > 0)
                foreach(var t in post.Translations)
                    doc.Add(new Field(t.Key.ToString().ToLowerInvariant(), t.Value.TrimEnd('/'), Field.Store.YES, Field.Index.NO));

            if (structuredDataTranslator != null)
            {
                post.AcceptTranslator(structuredDataTranslator);
                var structuredData = structuredDataTranslator.Output;
                var jsonld = structuredData.ToJsonLD();
                if(!jsonld.IsBlank()) doc.Add(new Field(IndexFields.StructuredDataJsonLD, jsonld, Field.Store.YES, Field.Index.NO));
            }

            return doc;
        }

        private Document CreateDocument(Category category)
        {
            Document doc = new Document();
            doc.Add(new Field(IndexFields.BlogID, category.Blog.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.Locale, category.Blog.Locale.ToString().ToLowerInvariant(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.Title, category.Title, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.URN, category.URN.TrimEnd('/'), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field(IndexFields.Tags, "cat", Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            if (category.Translations != null && category.Translations.Any())
                foreach (var t in category.Translations)
                    doc.Add(new Field(t.Locale.ToString().ToLowerInvariant(), t.URN.TrimEnd('/'), Field.Store.YES, Field.Index.NO));

            return doc;
        }

        private static string PrepareSnippet(string source)
        {
            string snippet = source.Trim()
                                    .GetFirstHtmlParagraph()
                                    .StripHTMLTags()
                                    .StripLineFeedCarriageReturn()
                                    .StripConsecutiveSpaces();

            if (snippet.Length > IndexSettings.MaxSnippetLength)
                return snippet.Substring(0, IndexSettings.MaxSnippetLength);

            return snippet;
        }        
    }
}
