extern alias SpeckleNewtonsoft;
using SpeckleNewtonsoft.Newtonsoft.Json;
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

    public bool _showSigninSuccess = true;
    public bool showSigninsuccess
    {
      get { return _showSigninSuccess; }
      set { _showSigninSuccess = value; showMainLogin = !value; OnPropertyChanged( "showSigninsuccess" ); OnPropertyChanged( "showMainLogin" ); }
    }

    public bool showMainLogin { get; set; } = false;

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
      showSigninsuccess = true;
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

    private void DeleteAccount( object sender, RoutedEventArgs e )
    {
      var bt = sender as Button;
      LocalContext.RemoveAccount( bt.DataContext as Account );
      LoadAccounts();
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

    private void SignInClick( object sender, RoutedEventArgs e )
    {
      Task.Run( ( ) =>
      {
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

        SaveOrUpdateAccount( new Account() { RestApi = apiCl.BaseUrl, Email = email, Token = apiToken } );

        Dispatcher.Invoke( ( ) =>
        {
          defaultServer = "";
          showSigninsuccess = true;
          isInRequestFlow = false;
          LoadAccounts();
        } );
      }
      catch ( Exception e )
      {
        Debug.WriteLine( e );
      }
    }

    public void SaveOrUpdateAccount( Account newAccount )
    {
      var existingAccounts = LocalContext.GetAllAccounts();
      var newUri = new Uri( newAccount.RestApi );

      var serverName = GetServerName( newUri );

      foreach ( var acc in existingAccounts )
      {
        var eUri = new Uri( acc.RestApi );
        if ( ( eUri.Host == newUri.Host ) && ( acc.Email == newAccount.Email ) && ( eUri.Port == newUri.Port ) )
        {
          acc.ServerName = serverName;
          acc.Token = newAccount.Token;
          LocalContext.RemoveAccount( acc ); // TODO: Add update account method, as this is rather stupid
          LocalContext.AddAccount( acc );
          return;
        }
      }
      newAccount.ServerName = serverName;
      LocalContext.AddAccount( newAccount );
    }

    public string GetServerName( Uri serverApi )
    {
      using ( var cl = new WebClient() )
      {
        var response = JsonConvert.DeserializeObject<dynamic>( cl.DownloadString( serverApi ) );
        return response.serverName as string;
      }
      throw new Exception( "Could not get server name." );
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

    private void ReturnToMain( object sender, RoutedEventArgs e )
    {
      showSigninsuccess = false;
    }
  }
}
