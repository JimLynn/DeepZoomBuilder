using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DeepZoomBuilder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Source.Text) == false && string.IsNullOrEmpty(Destination.Text) == false)
            {
                var creator = new DeepZoomCreator();
                creator.CreateSingleComposition(Source.Text, Destination.Text, ImageType.Jpeg);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.DefaultExt = "jpg";
            dlg.Filter = "Images|*.jpeg;*.jpg;*.png";
            if (dlg.ShowDialog() == true)
            {
                Source.Text = dlg.FileName;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = "dzi";
            dlg.Filter = "DZI (*.dzi)|*.dzi|XML (*.xml)|*.xml";
            if (dlg.ShowDialog() == true)
            {
                Destination.Text = dlg.FileName;
            }
        }
    }
}
