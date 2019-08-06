using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
  public partial class SignInWindow : Window, INotifyPropertyChanged
  {
    public string _defaultServer = "https://dev.hestia.speckle.works";
    public string defaultServer
    {
      get { return _defaultServer; }
      set { _defaultServer = value; }
    }

    public bool _isCorrectUrl = true;
    public bool isCorrectUrl
    {
      get { return _isCorrectUrl; }
      set { _isCorrectUrl = value; OnPropertyChanged( "isCorrectUrl" ); }
    }

    public string _errorMessage = "Url check result.";
    public string errorMessage
    {
      get { return _errorMessage; }
      set { _errorMessage = value; OnPropertyChanged( "errorMessage" ); }
    }

    private Timer GetApiTimer;

    internal ObservableCollection<Account> accounts = new ObservableCollection<Account>();
    public string restApi;
    public string apitoken;

    public bool isInRequestFlow = false;

    Process browser;
    HttpListener listener;

    public SignInWindow( )
    {
      this.DataContext = this;

      InitializeComponent();
      LoadAccounts();

      GetApiTimer = new Timer( 500 ) { Enabled = false, AutoReset = false };
      GetApiTimer.Elapsed += GetApiTimer_Elapsed;
      GetApiTimer.Start();
    }

    private void GetApiTimer_Elapsed( object sender, ElapsedEventArgs e )
    {
      try
      {
        var baseUri = new Uri( defaultServer );
        var apiUri = baseUri.Scheme + "://" + baseUri.Host;

        if ( !baseUri.IsDefaultPort ) { apiUri += ":" + baseUri.Port; }

        apiUri += "/api";

        var request = ( HttpWebRequest ) WebRequest.Create( new Uri( apiUri ) );
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        using ( HttpWebResponse response = ( HttpWebResponse ) request.GetResponse() )
        using ( Stream stream = response.GetResponseStream() )
        using ( StreamReader reader = new StreamReader( stream ) )
        {
          var test = reader.ReadToEnd();
          var tes2 = test;
          if ( test.Contains( "isSpeckleServer" ) )
          {
            isCorrectUrl = true;
            errorMessage = "Server url ok (got correct api response).";
          }
          else
          {
            isCorrectUrl = false;
            errorMessage = "There seems to be no speckle server there.";
          }
        }
      }
      catch ( Exception err )
      {
        errorMessage = "There seems to be no speckle server there.";
        isCorrectUrl = false;
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged( string propertyName )
    {
      PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
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
      Task.Run( ( ) =>
      {
        //browser = Process.Start( "https://dev.hestia.speckle.works/signin?redirectUrl=http://localhost:5050" );

        var baseUri = new Uri( defaultServer );
        var apiUri = baseUri.Scheme + "://" + baseUri.Host;

        if ( !baseUri.IsDefaultPort ) { apiUri += ":" + baseUri.Port; }

        browser = Process.Start( this.defaultServer + "/signin?redirectUrl=http://localhost:5050" );
        isInRequestFlow = true;

        InstantiateWebServer();
      } ); //NOTE: lookup cancellation tokens and the like
    }

    private void InstantiateWebServer( )
    {
      if ( listener != null )
      {
        listener.Abort();
      }
      try
      {
        listener = new HttpListener();
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
        MessageBox.Show( String.Format( "Hello, {0}! You've signed-in succesfully with your NOT FAKE email {2}. Well done.", res.Resource.Name, res.Resource.Apitoken, email ) );

        // TODO: try save this as a new account if it doesn't exist, update otherwise. 
        isInRequestFlow = false;
      }
      catch ( Exception e )
      {
        Debug.WriteLine( e );
      }
    }

    private void serverUrlTextChanged( object sender, TextChangedEventArgs e )
    {
      defaultServer = ( ( TextBox ) sender ).Text;
      try
      {
        var testUri = new Uri( defaultServer );
        GetApiTimer.Start();
      }
      catch
      {
        isCorrectUrl = false;
        errorMessage = "That's not a valid url. Forgot the https:// ?";
      }
    }
  }
}
