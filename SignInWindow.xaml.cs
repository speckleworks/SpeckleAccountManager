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
#if Debug
    private string _defaultServer = "https://dev.hestia.speckle.works";
#endif
    private string _defaultServer = "https://hestia.speckle.works";
    public string defaultServer
    {
      get { return _defaultServer; }
      set { _defaultServer = value; OnPropertyChanged("defaultServer"); }
    }

    private bool _isCorrectUrl = true;
    public bool isCorrectUrl
    {
      get { return _isCorrectUrl; }
      set { _isCorrectUrl = value; OnPropertyChanged("isCorrectUrl"); }
    }

    private string _errorMessage = "Url check result.";
    public string errorMessage
    {
      get { return _errorMessage; }
      set { _errorMessage = value; OnPropertyChanged("errorMessage"); }
    }

    private bool _showSigninSuccess = false;
    public bool showSigninsuccess
    {
      get { return _showSigninSuccess; }
      set { _showSigninSuccess = value; showMainLogin = !value; OnPropertyChanged("showSigninsuccess"); OnPropertyChanged("showMainLogin"); }
    }

    private bool _hasAccounts = false;
    public bool hasAccounts
    {
      get { return accounts.Any(); }
      set { _hasAccounts = value; }
    }

    private bool _hasMultipleAccounts = false;
    public bool hasMultipleAccounts
    {
      get { return accounts.Count > 1; }
      set { _hasMultipleAccounts = value; }
    }


    public bool showMainLogin { get; set; } = true;

    private DebounceDispatcher debounceTimer = new DebounceDispatcher();

    private string _apiUri { get; set; }
    private string _serverUri { get; set; }

    private ObservableCollection<Account> _accounts = new ObservableCollection<Account>();
    public ObservableCollection<Account> accounts
    {
      get { return _accounts; }
      set
      {
        _accounts = value; OnPropertyChanged("accounts");
      }
    }


    public string restApi;
    public string apitoken;

    public bool isInRequestFlow = false;

    Process browser;
    HttpListener listener;

    //true if it's a popup wind0w. it will close automatically after selecting an account
    bool _isPopup;

    public SignInWindow(bool isPopup = false)
    {
      this.DataContext = this;

      _isPopup = isPopup;

      InitializeComponent();
      accounts.CollectionChanged += Accounts_CollectionChanged;

      LoadAccounts();
    }

    private void Accounts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
      OnPropertyChanged("hasAccounts");
      OnPropertyChanged("hasMultipleAccounts");
    }

    private async Task CheckServerUrl(string url)
    {
      Uri baseUri;
      bool result = Uri.TryCreate(url, UriKind.Absolute, out baseUri)
          && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps);

      if (!result)
      {
        isCorrectUrl = false;
        errorMessage = @"Hey, that's not a valid url!";
        return;
      }

      try
      {
        _serverUri = baseUri.Scheme + "://" + baseUri.Host;

        if (!baseUri.IsDefaultPort) { _serverUri += ":" + baseUri.Port; }

        _apiUri = _serverUri + "/api";

        var request = (HttpWebRequest)WebRequest.Create(new Uri(_apiUri));
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream))
        {
          var test = reader.ReadToEnd();
          var tes2 = test;
          if (test.Contains("isSpeckleServer"))
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
      catch (Exception err)
      {
        errorMessage = "There seems to be no speckle server there.";
        isCorrectUrl = false;
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    private void LoadAccounts()
    {
      var acc = LocalContext.GetAllAccounts();
      accounts.Clear();
      foreach (var a in acc)
      {
        accounts.Add(a);
      }

      if (accounts.Any(x => x.IsDefault))
      {
        int index = accounts.Select((v, i) => new { acc = v, index = i }).First(x => x.acc.IsDefault).index;
        defaultAccountBox.SelectedIndex = index;
      }
    }


    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
      var rb = sender as RadioButton;
      LocalContext.SetDefaultAccount(rb.DataContext as Account);
    }

    private void AccountListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
      try
      {
        restApi = accounts[AccountListBox.SelectedIndex].RestApi;
        apitoken = accounts[AccountListBox.SelectedIndex].Token;
      }
      catch
      {

      }
      //this.Close();
    }

    private void SignInClick(object sender, RoutedEventArgs e)
    {
      Task.Run(() =>
     {
       browser = Process.Start(_serverUri + "/signin?redirectUrl=http://localhost:5050");
       isInRequestFlow = true;

       InstantiateWebServer();
     });
    }

    private void InstantiateWebServer()
    {
      if (listener != null)
      {
        listener.Abort();
      }
      try
      {
        listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:5050/");
        listener.Start();

        var ctx = listener.GetContext();
        //this is where it stops to wait for a request
        var req = ctx.Request;
        listener.Stop();

        try
        {
          browser.CloseMainWindow();
          browser.Close();
        }
        catch (Exception e)
        {
          Debug.WriteLine(e);
        }

        AddAccountFromRedirect(ctx.Request.Url.Query);


      }
      catch (Exception e)
      {
        Debug.WriteLine(e);
      }
    }

    private void AddAccountFromRedirect(string redirect)
    {
      var myString = Uri.UnescapeDataString(redirect);

      var splitRes = myString.Replace("?token=", "").Split(new[] { ":::" }, StringSplitOptions.None);
      var token = splitRes[0];
      //var serverUrl = splitRes[1];

      var apiCl = new SpeckleApiClient(_apiUri) { AuthToken = token };
      var res = apiCl.UserGetAsync().Result;

      var apiToken = res.Resource.Apitoken;
      var email = res.Resource.Email;

      SaveOrUpdateAccount(new Account() { RestApi = apiCl.BaseUrl, Email = email, Token = apiToken });

      Dispatcher.Invoke(() =>
      {
        //defaultServer = "";
        showSigninsuccess = true;
        isInRequestFlow = false;
        LoadAccounts();
      });
    }

    public void SaveOrUpdateAccount(Account newAccount)
    {
      var existingAccounts = LocalContext.GetAllAccounts();
      var newUri = new Uri(newAccount.RestApi);

      var serverName = GetServerName(newUri);

      foreach (var acc in existingAccounts)
      {
        var eUri = new Uri(acc.RestApi);
        if ((eUri.Host == newUri.Host) && (acc.Email == newAccount.Email) && (eUri.Port == newUri.Port))
        {
          acc.ServerName = serverName;
          acc.Token = newAccount.Token;
          LocalContext.RemoveAccount(acc); // TODO: Add update account method, as this is rather stupid
          LocalContext.AddAccount(acc);
          return;
        }
      }
      newAccount.ServerName = serverName;
      LocalContext.AddAccount(newAccount);
    }

    public string GetServerName(Uri serverApi)
    {
      using (var cl = new WebClient())
      {
        var response = JsonConvert.DeserializeObject<dynamic>(cl.DownloadString(serverApi));
        return Convert.ToString(response.serverName);
      }
      throw new Exception("Could not get server name.");
    }

    private void serverUrlTextChanged(object sender, TextChangedEventArgs e)
    {
      debounceTimer.Debounce(2000, async parm =>
      {
        await CheckServerUrl(((TextBox)sender).Text);
      });
    }

    private void ReturnToMain(object sender, RoutedEventArgs e)
    {
      showSigninsuccess = false;
    }

    private void ClearDefaultAccount(object sender, RoutedEventArgs e)
    {
      defaultAccountBox.SelectedIndex = -1;
      LocalContext.ClearDefaultAccount();

    }

    private void RemoveAccount_Executed(object sender, ExecutedRoutedEventArgs e)
    {
      var a = e.Parameter as Account;
      if (MessageBox.Show($"Are you sure you want to remove '{a.Email}' on '{a.ServerName}' from this machine?", "Remove account?", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
      {
        if (a.IsDefault)
        {
          ClearDefaultAccount(null, null);
        }
        LocalContext.RemoveAccount(a);
        accounts.Remove(a);
      }
    }

    private void DefaultAccountBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (defaultAccountBox.SelectedIndex != -1)
      {
        LocalContext.SetDefaultAccount(accounts[defaultAccountBox.SelectedIndex]);
      }
    }

    private void AccountListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
      if (AccountListBox.SelectedIndex != -1)
      {
        this.Close();
      }
    }

    private void ManuallyAddAccountClick(object sender, RoutedEventArgs e)
    {
      var win = new ConnectionStringWin();
      var result = win.ShowDialog();
      if (result.HasValue && result.Value && !string.IsNullOrEmpty(win.connectionString.Text))
      {
        try
        {
          AddAccountFromRedirect(win.connectionString.Text);
        }
        catch (Exception ex)
        {
          Debug.WriteLine(ex);
        }

      }
    }
  }
}
