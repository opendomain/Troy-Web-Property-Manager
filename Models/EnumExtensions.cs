using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Troy_Web_Property_Manager.Models
{
    public static class EnumExtensions
    {
        /// <summary>The value's [Display(Name)] if it has one, otherwise its name.</summary>
        public static string DisplayName(this Enum value)
        {
            return value.GetType().GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
        }
    }
}
