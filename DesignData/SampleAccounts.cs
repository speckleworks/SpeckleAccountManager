using SpeckleCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpecklePopup.DesignData
{
  public class SampleAccounts
  {
    public ObservableCollection<Account> accounts { get; set; }

    public string defaultServer { get; set; }
  }
}
