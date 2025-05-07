using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LapTrinhWindows.Models.DTO
{
    

    public class ImageFileDTO
    {
        public IFormFile? ImageFile { get; set; }

        [Required]
        public int OrdinalNumbers { get; set; }
    }
    

    public class UpsertProductImagesDTO
    {
        [Required]
        public int ProductID { get; set; }
        public List<ImageFileDTO> Images { get; set; } = new List<ImageFileDTO>();
    }
}