﻿using System;
using Xunit;
using Composr.Core;
using System.Transactions;
using System.Collections.Generic;
using FizzWare.NBuilder;

namespace Composr.Tests
{
    
    public class PostRepositoryTests
    {
        //to be created for next session
        private Composr.Core.Repositories.IRepository<Post> repo;

        public PostRepositoryTests()
        {
            repo = new Composr.Repository.Sql.PostRepository(new Blog(1));
        }

        [Fact]
        public void PostRepositoryGetsPostOfSameLocale()
        {
            using (TransactionScope t = new TransactionScope())
            {
                Post post1 = CreateNewPost();
                int id = repo.Save(post1);
                Post post2 = repo.Get(id);
                
                Assert.True(post1.Body.Equals(post2.Body));
                Assert.True(post1.Blog.Id.Equals(post2.Blog.Id));
                Assert.True(post2.Id == id);
                Assert.True(post1.Status == post2.Status);
                Assert.True(post1.Title.Equals(post2.Title));
            }
        }

        [Fact]
        public void PostRepositoryGetsPostList()
        {
            using (TransactionScope t = new TransactionScope())
            {
                Blog blog = CreateBlog();
                int existing = GetExistingRepoCount();
                int newposts = 5;

                IList<Post> l1 = Builder<Post>
                        .CreateListOfSize(newposts)
                        .WhereAll().AreConstructedUsing<Post>(()=>new Post(blog)).And(p => p.Id = null)
                        .Build();
                                
                repo.Locale = Locale.EN;
                foreach (Post p in l1) repo.Save(p);
                IList<Post> l2 = repo.Get(null);
                Assert.True(l2.Count == newposts + existing);                
            }
        }

        private int GetExistingRepoCount()
        {
            return 0;
        }

        [Fact]
        public void PostRepositoryUpdatesPost()
        {
            using (TransactionScope t = new TransactionScope())
            {
                int id = repo.Save(CreateNewPost());
                Post post1 = repo.Get(id);
                post1.Title = "new title";
                repo.Save(post1);
                Post post2 = repo.Get(id);
                Assert.True(post1.Title.Equals(post2.Title));
            }
        }

        [Fact]
        public void PostRepositoryCanDeletePosts()
        {
            using (TransactionScope t = new TransactionScope())
            {
                int id = repo.Save(CreateNewPost());
                Post p1 = repo.Get(id);
                
                repo.Delete(p1);
                repo.Locale = p1.Blog.Locale.Value;
                
                Post p2 = repo.Get(id);
                Assert.Null(p2);
            }
        }

        [Fact]
        public void PostRepositoryCanCount()
        {
            Blog blog = CreateBlog();
            int existing = GetExistingRepoCount();
            int newposts = 5;

            IList<Post> list = Builder<Post>
                        .CreateListOfSize(newposts)
                        .WhereAll().AreConstructedUsing<Post>(() => new Post(blog)).And(p => p.Id = null)
                        .Build();

            using (TransactionScope t = new TransactionScope())
            {
                int count = 0;
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);
                count = repo.Count(null);
                Assert.True(count == newposts + existing);                             
            }
        }

        [Fact]
        public void PostRepositoryCanRetrievePostByIDAndLocale()
        {
            using (TransactionScope t = new TransactionScope())
            {
                Post p1 = CreateNewPost();
                int id = repo.Save(p1);
                Post p2 = repo.Get(id);
                Assert.True(p2.Title.Equals(p1.Title));
                Assert.True(p2.Body.Equals(p1.Body));
                Assert.True(p2.Status.Equals(p1.Status));
            }
        }

        [Fact]
        public void PostRepositoryGetsPostsMatchingTitle()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);                
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter { Criteria = "auto" });
                Assert.True(posts.Count == 3);
            }
        }

        [Fact]
        public void PostRepositoryGetsPostsMatchingBody()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter { Criteria = "your" });
                Assert.True(posts.Count == 2);
            }
        }

        [Fact]
        public void PostRepositoryGetsPostsMatchingTitleOrBody()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter { Criteria = "sell" });
                Assert.True(posts.Count == 3);
            }
        }

        [Fact]
        public void PostRepositorySupportsPagination()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter { Criteria = "auto", Limit = 10, Offset = 1 });
                Assert.True(posts[0].Title.Equals("das auto"));
                Assert.True(posts[1].Title.Equals("motoautomatic"));
                Assert.True(posts.Count == 2);
            }
        }

        [Fact]
        public void PostRepositoryRetrievesPostsFromExistingBlogsOnly()
        {
            Blog nonexistingblog = new Blog(2);
            Composr.Core.Repositories.IRepository<Post> repo2 = new Composr.Repository.Sql.PostRepository(nonexistingblog);
            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter());
                Assert.True(posts.Count == 0);
            }
        }

        [Fact]
        public void PostRepositoryDoesNotRetrieveDeletedPosts()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                int id, count = 0;
                List<int> deleted = new List<int>();

                foreach (Post p in list)
                {
                    id = repo.Save(p);
                    count++;
                    if (count > 3) deleted.Add(id);
                }

                foreach (int deletedid in deleted) repo.Delete(new Post(blog) { Id = deletedid });
                
                Assert.True(repo.Count(null) == (list.Count - deleted.Count));
            }
        }

        [Fact]
        public void PostRepositoryIsSQLInjectionSafe()
        {
            Blog blog = CreateBlog();
            IList<Post> list = MockPostList(blog);

            using (TransactionScope t = new TransactionScope())
            {
                repo.Locale = Locale.EN;
                foreach (Post p in list) repo.Save(p);
                IList<Post> posts = repo.Get(new Composr.Core.Repositories.Filter { Criteria = "'auto' OR 1=1 ", Limit = 10, Offset = 1 });
                Assert.True(posts.Count == 0);
            }
        }

        private static IList<Post> MockPostList(Blog blog)
        {
            IList<Post> list = Builder<Post>
                    .CreateListOfSize(5)
                    .WhereTheFirst(1).IsConstructedUsing(() => new Post(blog)).And(x => x.Title = "autotrader").And(x => x.Body = "sell your cars")
                    .AndTheNext(1).IsConstructedUsing(() => new Post(blog)).And(x => x.Title = "das auto").And(x => x.Body = "volkswagen is diesel")
                    .AndTheNext(1).IsConstructedUsing(() => new Post(blog)).And(x => x.Title = "motoautomatic").And(x => x.Body = "upsell your car")
                    .AndTheNext(1).IsConstructedUsing(() => new Post(blog)).And(x => x.Title = "man utd").And(x => x.Body = "football club")
                    .AndTheNext(1).IsConstructedUsing(() => new Post(blog)).And(x => x.Title = "maple sell leafs").And(x => x.Body = "never win anything")
                    .WhereAll().IsConstructedUsing(() => new Post(blog)).And(p => p.Id = null)
                    .Build();
            return list;
        }

        private Post CreateNewPost()
        {
            return new Post(new Blog(1))
            {
                Title = "test post title",
                Body = "this is the post's body",
                Status = PostStatus.DRAFT                
            };
        }

        private Blog CreateBlog()
        {
            return new Blog(1);
        }
    }
}
