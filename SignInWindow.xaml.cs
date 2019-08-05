using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SpeckleCore;

namespace SpecklePopup
{
  /// <summary>
  /// Interaction logic for SignInWindow.xaml
  /// </summary>
  public partial class SignInWindow : Window
  {

    private string defaultServer = "https://dev.hestia.speckle.works";
    private List<string> existingServers = new List<string>();
    private List<string> existingServers_fullDetails = new List<string>();
    internal ObservableCollection<Account> accounts = new ObservableCollection<Account>();
    private bool validationCheckPass = false;
    private Uri ServerAddress;
    private string email;
    private string password;
    private string serverName;
    public string restApi;
    public string apitoken;

    Process browser;

    public SignInWindow( )
    {
      InitializeComponent();
      LoadAccounts();
    }


    private void LoadAccounts( )
    {
      accounts = new ObservableCollection<Account>( LocalContext.GetAllAccounts() );
      AccountListBox.ItemsSource = accounts;

      if ( accounts.Any( x => x.IsDefault ) )
      {
        int index = accounts.Select( ( v, i ) => new { acc = v, index = i } ).First( x => x.acc.IsDefault ).index;
        AccountListBox.SelectedIndex = index;
      }
    }


    private void RadioButton_Click( object sender, RoutedEventArgs e )
    {
      var rb = sender as RadioButton;
      LocalContext.SetDefaultAccount( rb.DataContext as Account );
    }

    private void AccountListBox_MouseDoubleClick( object sender, MouseButtonEventArgs e )
    {
      try
      {
        restApi = accounts[ AccountListBox.SelectedIndex ].RestApi;
        apitoken = accounts[ AccountListBox.SelectedIndex ].Token;
      }
      catch
      {

      }
      //this.Close();
    }

    private void Button_Click( object sender, RoutedEventArgs e )
    {
      browser = Process.Start( "https://dev.hestia.speckle.works/signin?redirectUrl=http://localhost:5050" );
      InstantiateWebServer();
    }

    private void InstantiateWebServer( )
    {
      HttpListener listener = new HttpListener();
      listener.Prefixes.Add( "http://localhost:5050/" );
      listener.Start();

      var ctx = listener.GetContext();
      var req = ctx.Request;
      listener.Stop();

      try
      {
        browser.CloseMainWindow();
        browser.Close();
      }
      catch ( Exception e )
      {
        Debug.WriteLine( e );
      }
      var myString = Uri.UnescapeDataString( ctx.Request.Url.Query );

      Debug.WriteLine( myString );
      var splitRes = myString.Replace( "?token=", "" ).Split( new[ ] { ":::" }, StringSplitOptions.None );
      var token = splitRes[ 0 ];
      var serverUrl = splitRes[ 1 ];

      var apiCl = new SpeckleApiClient( serverUrl + "/api" ) { AuthToken = token };
      var res = apiCl.UserGetAsync().Result;

      var apiToken = res.Resource.Apitoken;
      var email = res.Resource.Email;
      
    }
  }
}
