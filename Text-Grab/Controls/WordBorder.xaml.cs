using System;
using System.Collections.Generic;
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

namespace Text_Grab.Controls
{
    /// <summary>
    /// Interaction logic for WordBorder.xaml
    /// </summary>
    public partial class WordBorder : UserControl
    {
        public bool IsSelected { get; set; } = false;

        public string Word { get; set; } = "";

        public WordBorder()
        {
            InitializeComponent();
        }

        public void Select()
        {
            IsSelected = true;
            this.BorderBrush = new SolidColorBrush(Colors.Yellow);
        }

        public void Deselect()
        {
            IsSelected = false;
            this.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 48, 142, 152));
        }

        private void WordBorderControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (IsSelected)
                Deselect();
            else
                Select();
        }

        private async void WordBorderControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(Word);

            if(IsSelected)
            {
                await Task.Delay(100);
                Deselect();
            }
            else
            {
                await Task.Delay(100);
                Select();
            }
        }
    }
}
