using System.ComponentModel.DataAnnotations;

namespace FH_Axure_UI_Converter.Models;

public class FileUploadViewModel
{
    [Required]
    public List<IFormFile> HtmlFiles { get; set; }

    [Required(ErrorMessage = "Please specify the directory to save the file.")]
    [Display(Name = "Save Directory")]
    public string SaveDirectory { get; set; }
    public string? UiSaveDirectory { get; set; }
}
