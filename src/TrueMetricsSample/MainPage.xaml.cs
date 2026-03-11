using Xamarin.Forms;
using TrueMetricsSample.ViewModels;

namespace TrueMetricsSample
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            BindingContext = new MainViewModel();
        }
    }
}
