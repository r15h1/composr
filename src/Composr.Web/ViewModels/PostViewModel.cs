﻿using Composr.Core;
using System.ComponentModel.DataAnnotations;

namespace Composr.Web.ViewModels
{
    public class PostViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage ="Title is missing")]
        public string Title { get; set; }
        
        [Required(ErrorMessage = "Body is missing")]
        public string Body { get; set; }

        [StringLength(maximumLength:155, MinimumLength = 115)]
        public string MetaDescription { get; set; }

        public int? BlogId { get; set; }

        public PostStatus PostStatus { get; set; }

        [StringLength(250)]
        public string URN { get; set; }

        public string Tags { get; set; }

        public string ImageUrl { get; set; }

        public string ImageCaption { get; set; }

        public string Language { get; set; }
    }
}
