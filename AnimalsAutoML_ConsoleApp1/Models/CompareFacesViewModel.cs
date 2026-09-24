using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AnimalsAutoML_ConsoleApp1.Models;

public class CompareFacesViewModel : IValidatableObject
{
    [Display(Name = "Перше фото")]
    public IFormFile? FirstPhoto { get; set; }

    [Display(Name = "Друге фото")]
    public IFormFile? SecondPhoto { get; set; }

    [Display(Name = "Посилання на перше фото")]
    [StringLength(4096, ErrorMessage = "Посилання занадто довге.")]
    public string? FirstPhotoUrl { get; set; }

    [Display(Name = "Посилання на друге фото")]
    [StringLength(4096, ErrorMessage = "Посилання занадто довге.")]
    public string? SecondPhotoUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((FirstPhoto is not null) == !string.IsNullOrWhiteSpace(FirstPhotoUrl))
            yield return new ValidationResult("Перше фото: оберіть файл або вкажіть посилання — лише одне джерело.", [nameof(FirstPhotoUrl)]);
        if ((SecondPhoto is not null) == !string.IsNullOrWhiteSpace(SecondPhotoUrl))
            yield return new ValidationResult("Друге фото: оберіть файл або вкажіть посилання — лише одне джерело.", [nameof(SecondPhotoUrl)]);
    }

    [BindNever] public ImageQualityResult? FirstQuality { get; set; }
    [BindNever] public ImageQualityResult? SecondQuality { get; set; }
    [BindNever] public bool Compared { get; set; }
    [BindNever] public float? Similarity { get; set; }
}
