﻿using Composr.Core;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Composr.Repository.Sql
{
    public class PostRepository: IPostRepository
    {
        private ISpecification<Post> specification;
        private IBlogRepository blogRepository;

        public PostRepository(Blog blog, ISpecification<Post> specification, IBlogRepository blogRepository) 
        {
            if (blog == null || specification == null) throw new ArgumentNullException();
            this.specification = specification;
            this.blogRepository = blogRepository;
            Blog = blog;
            Locale = blog.Locale.Value;
        }

        public Blog Blog { get; private set; }

        public Core.Locale Locale { get; set; }

        public Core.Post Get(int id)
        {
            return Fetch(id, Locale);
        }

        private Core.Post Fetch(int postid, Core.Locale locale)
        {
            Core.Post post = null;
            var p = new DynamicParameters();
            p.Add("@LocaleID", (int)locale);
            p.Add("@PostID", postid);

            using (System.Data.IDbConnection conn = ConnectionFactory.CreateConnection())
            {
                using (var reader = conn.QueryMultiple("Post_Select_One", p, commandType: System.Data.CommandType.StoredProcedure))
                {
                    post = reader.Read().Select<dynamic, Post>(row => BuildPost(row)).SingleOrDefault();
                    if (post != null)
                    {
                        post.Attributes = reader.Read().Select<dynamic, KeyValuePair<string, string>>(row => BuildAttributes(row)).ToDictionary(x => x.Key, x => x.Value);
                        post.Images = reader.Read().Select<dynamic, PostImage>(row => BuildImage(row)).ToList();
                    }
                };
                return post;
            }
        }

        private PostImage BuildImage(dynamic row)
        {
            return new PostImage { Caption = row.Caption, Url = row.ImageUrl, SequenceNumber = row.SequenceNumber };
        }

        private KeyValuePair<string, string> BuildAttributes(dynamic row)
        {
            return new KeyValuePair<string, string>(row.Key, row.Value);
        }

        private Post BuildPost(dynamic row)
        {            
            Blog blog = Blog;
            Locale postLocale = (Core.Locale)Enum.Parse(typeof(Core.Locale), row.LocaleID.ToString());

            if (Blog.Locale != postLocale)
            {
                blogRepository.Locale = postLocale;
                blog = blogRepository.Get(Blog.Id.Value);
            }

            return new Composr.Core.Post(blog)
            {
                Body = row.Body,
                Id = row.PostID,
                Status = (Core.PostStatus)Enum.Parse(typeof(Core.PostStatus), row.PostStatusID.ToString()),
                Title = row.Title,
                URN = row.URN,
                DatePublished = row.DatePublished,
                DateCreated = row.DateCreated
            };
        }

        public IList<Core.Post> Get(Filter filter)
        {
            if (filter == null) filter = new Filter();
            return Fetch("Post_Select_Many", filter.Criteria, Locale, filter.Offset, filter.Limit);
        }

        private IList<Post> Fetch(string command, string criteria, Locale locale, int? offset, int? limit)
        {
            List<Post> posts;
            
            var p = new DynamicParameters();
            p.Add("@BlogID", Blog.Id);
            p.Add("@Criteria", criteria);
            p.Add("@LocaleID", (int)locale);

            if (offset.HasValue) p.Add("@Offset", offset.Value);
            if (limit.HasValue) p.Add("@Limit", limit.Value);

            using (System.Data.IDbConnection conn = ConnectionFactory.CreateConnection())
            {
                using (var reader = conn.QueryMultiple(command, p, commandType: System.Data.CommandType.StoredProcedure))
                {
                    posts = reader.Read().Select<dynamic, Post>(row => BuildPost(row)).ToList();
                    var attributes = (
                        from a in reader.Read()
                        group a by a.PostID into postAttributes
                        select postAttributes).ToDictionary(attr => (int) attr.Key, attr => attr.ToDictionary(x=> (string) x.Key, x=> (string) x.Value)
                    );

                    var images = (
                        from a in reader.Read()
                        group a by a.PostID into postImages
                        select postImages).ToDictionary(img => (int)img.Key, img => (PostImage) img.Select(x => BuildImage(x)).FirstOrDefault()
                    );

                    Dictionary<int, Dictionary<Locale, string>> translations = null;
                    if (!reader.IsConsumed)
                    {
                        //get translated urns
                        //var translations = reader.Read().ToDictionary(row => (Locale)row.LocaleID, row => (string)row.URN);
                        translations = (
                            from a in reader.Read()
                            group a by a.PostID into postTranslations
                            select postTranslations).ToDictionary(trans => (int)trans.Key, trans => trans.ToDictionary(x => (Locale)x.LocaleID, x => (string)x.URN)
                        );
                    }

                    posts.ForEach((post) =>
                    {
                        Dictionary<string, string> attr = null;
                        PostImage pimg = null;
                        Dictionary<Locale, string> trans;

                        if (attributes.TryGetValue(post.Id.Value, out attr)) post.Attributes = attr;
                        if (images != null && images.TryGetValue(post.Id.Value, out pimg)) post.Images.Add(pimg);
                        if (translations != null && translations.TryGetValue(post.Id.Value, out trans)) post.Translations = trans;
                    });

                }
            }

            return posts;
        }

        private KeyValuePair<int, KeyValuePair<string, string>> BuildAttributeDictionary(dynamic row)
        {
            return new KeyValuePair<int, KeyValuePair<string, string>>(row.PostID, new KeyValuePair<string, string>(row.Key, row.Value));
        }


        public int Count(string criteria)
        {
            var p = new DynamicParameters();
            p.Add("@BlogID", Blog.Id);
            p.Add("@Criteria", criteria);
            p.Add("@LocaleID", (int)Locale);
            return QueryExecutor.ExecuteSingle<int>("Post_Count", p);
        }

        public int Save(Post post)
        {
            Validate(post);
            if (!post.Id.HasValue)
                return Create(post);

            Update(post);
            return post.Id.Value;
        }

        private void Validate(Post post)
        {
            if (post == null) throw new ArgumentNullException();
            var compliance = specification.EvaluateCompliance(post);
            if (!compliance.IsSatisfied) throw new SpecificationException(string.Join(Environment.NewLine, compliance.Errors));
        }

        private void Update(Post post)
        {
            var p = new DynamicParameters();
            p.Add("@BlogID", post.Blog.Id);
            p.Add("@PostID", post.Id);
            p.Add("@LocaleID", (int)post.Blog.Locale);
            p.Add("@Title", post.Title);
            p.Add("@Body", post.Body);
            p.Add("@PostStatusID", (int)post.Status);
            p.Add("@URN", post.URN);
            p.Add("@Attributes", GetPostAttributesDataTable(post.Attributes).AsTableValuedParameter("dbo.PostAttrributeTableType"));
            p.Add("@Images", GetPostImageDataTable(post.Images).AsTableValuedParameter("dbo.PostImageTableType"));
            QueryExecutor.ExecuteSingle<Post>("Post_Update", p);
        }

        /// <summary>
        /// inserts a new blog record in the db
        /// </summary>
        /// <param name="blog"></param>
        /// <returns></returns>
        private int Create(Core.Post post)
        {
            var p = new DynamicParameters();
            p.Add("@BlogID", post.Blog.Id);
            p.Add("@LocaleID", (int)post.Blog.Locale);
            p.Add("@Title", post.Title);
            p.Add("@Body", post.Body);
            p.Add("@PostStatusID", (int)post.Status);
            p.Add("@URN", post.URN);
            p.Add("@Attributes", GetPostAttributesDataTable(post.Attributes).AsTableValuedParameter("dbo.PostAttrributeTableType"));
            p.Add("@Images", GetPostImageDataTable(post.Images).AsTableValuedParameter("dbo.PostImageTableType"));
            return QueryExecutor.ExecuteSingle<int>("Post_Create", p);
        }

        private DataTable GetPostAttributesDataTable(IDictionary<string, string> attributes)
        {
            var dt = new DataTable();
            dt.Columns.Add("Key", typeof(string));
            dt.Columns.Add("Value", typeof(string));
            if (attributes != null && attributes.Count > 0)
                foreach (var attr in attributes)
                    dt.Rows.Add(attr.Key, attr.Value);
            
            return dt;
        }

        private DataTable GetPostImageDataTable(List<PostImage> images)
        {
            var dt = new DataTable();
            dt.Columns.Add("SequenceNumber", typeof(int));
            dt.Columns.Add("Url", typeof(string));
            dt.Columns.Add("Caption", typeof(string));

            if (images != null && images.Count > 0)
                foreach (var img in images)
                    if (!string.IsNullOrWhiteSpace(img.Url))
                        dt.Rows.Add(img.SequenceNumber, img.Url, img.Caption);

            return dt;
        }

        public void Delete(Post post)
        {
            if (post == null || !post.Id.HasValue) throw new ArgumentNullException();
            var p = new DynamicParameters();
            p.Add("@PostID", post.Id);
            p.Add("@LocaleID", (int)post.Blog.Locale);

            QueryExecutor.ExecuteSingle<Post>("Post_Delete", p);
        }

        public IList<Post> GetPublishedPosts(Filter filter)
        {
            if (filter == null) filter = new Filter();
            return Fetch("Post_Select_Published", filter.Criteria, Locale, filter.Offset, filter.Limit);
        }

        public IList<Post> GetTranslatedPosts(int postid)
        {
            return Fetch(postid);
        }

        /// <summary>
        /// get all translated versions of this post 
        /// </summary>
        /// <param name="postid"></param>
        /// <returns></returns>
        private IList<Core.Post> Fetch(int postid)
        {
            List<Post> posts;
            var p = new DynamicParameters();
            p.Add("@BlogID", Blog.Id);
            p.Add("@PostID", postid);

            using (System.Data.IDbConnection conn = ConnectionFactory.CreateConnection())
            {
                using (var reader = conn.QueryMultiple("Post_Select_Translations", p, commandType: System.Data.CommandType.StoredProcedure))
                {
                    posts = reader.Read().Select<dynamic, Post>(row => BuildPost(row)).ToList();                    
                };
                return posts;
            }
        }

        public void Translate(int postid, Locale locale)
        {
            var p = new DynamicParameters();
            p.Add("@BlogID", Blog.Id);
            p.Add("@PostID", postid);
            p.Add("@LocaleID", (int)locale);
            QueryExecutor.Execute("Post_Translate", p);            
        }
    }
}