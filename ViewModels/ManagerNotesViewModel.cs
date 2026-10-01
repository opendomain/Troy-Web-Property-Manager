using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Troy_Web_Property_Manager.ViewModels
{
    /// <summary>
    /// Property managers' private notes on an application - the notes panel and its edit modal.
    /// </summary>
    /// <remarks>
    /// <para>This is kept apart from <see cref="ApplicationEditorViewModel"/> on purpose. The editor view model is what
    /// applicants get, so the notes never go anywhere near it. The panel is a view component
    /// (<c>ManagerNotesViewComponent</c>) that loads its own data, managers only.</para>
    /// <para><see cref="Version"/> is posted back as a hidden field so the service can tell if another manager saved
    /// after this form was opened. <see cref="ApplicationId"/> comes from the route, and the "last updated" fields are
    /// display only.</para>
    /// </remarks>
    public class ManagerNotesViewModel
    {
        [BindNever] public int ApplicationId { get; set; }

        /// <summary>Same 2000-character limit as the column. Blank is fine - that clears the notes.</summary>
        [StringLength(2000)] public string? Notes { get; set; }

        /// <summary>The version the form was loaded with. Null if there were no notes yet.</summary>
        public Guid? Version { get; set; }

        /// <summary>Email of the manager who saved last. Display only.</summary>
        [BindNever] public string? UpdatedBy { get; set; }

        [BindNever] public DateTime? UpdatedAt { get; set; }
    }
}
