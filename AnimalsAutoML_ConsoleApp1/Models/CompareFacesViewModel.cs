using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AnimalsAutoML_ConsoleApp1.Models;

public class CompareFacesViewModel
{
    [Required(ErrorMessage = "Оберіть перше фото.")]
    [Display(Name = "Перше фото")]
    public IFormFile? FirstPhoto { get; set; }

    [Required(ErrorMessage = "Оберіть друге фото.")]
    [Display(Name = "Друге фото")]
    public IFormFile? SecondPhoto { get; set; }

    [BindNever] public ImageQualityResult? FirstQuality { get; set; }
    [BindNever] public ImageQualityResult? SecondQuality { get; set; }
    [BindNever] public bool Compared { get; set; }
    [BindNever] public float? Similarity { get; set; }
}
