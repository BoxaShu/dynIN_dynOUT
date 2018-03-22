using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace dynINOUT_UI
{

    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<Row> BindingList { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
           //Loaded += MainWindow_Loaded;
        }

        public void AddBlockNameList(ObservableCollection<string> blockNameList)
        {
            BindingList = new ObservableCollection<Row>();

            foreach (var i in blockNameList)
                BindingList.Add(new Row() { Key = i, Value=true});

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            this.Close();
        }
    }
}
