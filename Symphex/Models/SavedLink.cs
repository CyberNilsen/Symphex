using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Symphex.Models
{
    public partial class SavedLink : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public DateTime DateAdded { get; set; } = DateTime.Now;
        
        [ObservableProperty]
        private bool isFavorite = false;
        
        public string Category { get; set; } = "All";
    }
}
