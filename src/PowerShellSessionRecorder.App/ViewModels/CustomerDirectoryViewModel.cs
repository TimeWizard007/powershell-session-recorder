using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerShellSessionRecorder.App.Localization;
using PowerShellSessionRecorder.Core;

namespace PowerShellSessionRecorder.App.ViewModels;

public partial class CustomerDirectoryViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ICustomerCatalog _catalog;
    private readonly Action<string> _setTag;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly List<CustomerChoice> _customers = [];
    private string? _selectedId;
    private string? _publishedTag;
    private string? _editingId;
    private string? _pendingRemovalId;
    private string _formErrorKey = "";
    private bool _suspendSelection;

    public CustomerDirectoryViewModel(
        LocalizationService localization,
        ICustomerCatalog catalog,
        Action<string> setTag,
        Func<DateTimeOffset>? utcNow = null)
    {
        _localization = localization;
        _catalog = catalog;
        _setTag = setTag;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        Choices = [];
        VisibleCustomers = [];
        localization.LanguageChanged += (_, _) => RefreshText();
        Reload();
    }

    public ObservableCollection<CustomerChoice> Choices { get; }

    public ObservableCollection<CustomerChoice> VisibleCustomers { get; }

    [ObservableProperty]
    private CustomerChoice? _selectedCustomer;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isAdding;

    [ObservableProperty]
    private bool _isManaging;

    [ObservableProperty]
    private string _newTag = "";

    [ObservableProperty]
    private string _newDisplayName = "";

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editTag = "";

    [ObservableProperty]
    private string _editDisplayName = "";

    [ObservableProperty]
    private string _formError = "";

    [ObservableProperty]
    private string _catalogWarning = "";

    [ObservableProperty]
    private string _selectedTagText = "";

    [ObservableProperty]
    private string _pendingRemovalBody = "";

    [ObservableProperty]
    private string _statusMessage = "";

    public bool HasFormError => FormError.Length > 0;

    public bool HasCatalogWarning => CatalogWarning.Length > 0;

    public bool HasPendingRemoval => _pendingRemovalId is not null;

    public bool HasStatusMessage => StatusMessage.Length > 0;

    public bool ShowEmpty { get; private set; }

    public bool ShowNoMatches { get; private set; }

    public bool ShowCustomerList { get; private set; }

    partial void OnSelectedCustomerChanged(CustomerChoice? value)
    {
        if (_suspendSelection)
            return;

        _selectedId = value?.Id;
        PublishTag(value?.Tag ?? "");
        RefreshText();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnFormErrorChanged(string value) => OnPropertyChanged(nameof(HasFormError));

    partial void OnCatalogWarningChanged(string value) => OnPropertyChanged(nameof(HasCatalogWarning));

    partial void OnStatusMessageChanged(string value) => OnPropertyChanged(nameof(HasStatusMessage));

    partial void OnPendingRemovalBodyChanged(string value) => OnPropertyChanged(nameof(HasPendingRemoval));

    [RelayCommand]
    private void BeginAdd()
    {
        IsAdding = true;
        IsEditing = false;
        _editingId = null;
        NewTag = "";
        NewDisplayName = "";
        ClearFormError();
    }

    [RelayCommand]
    private void CancelAdd()
    {
        IsAdding = false;
        NewTag = "";
        NewDisplayName = "";
        ClearFormError();
    }

    [RelayCommand]
    private void SaveNew()
    {
        var result = Invoke(() => _catalog.Add(NewTag, NewDisplayName, _utcNow()));
        if (!ApplyEditResult(result))
            return;

        _selectedId = result.Customer!.Id;
        IsAdding = false;
        NewTag = "";
        NewDisplayName = "";
        StatusMessage = _localization.Get(L.StatusCustomerAdded);
        Reload();
    }

    [RelayCommand]
    private void ToggleManage()
    {
        IsManaging = !IsManaging;
        if (!IsManaging)
            CancelEdit();
    }

    [RelayCommand]
    private void BeginEdit(string id)
    {
        var customer = _customers.FirstOrDefault(choice => Same(choice.Id, id));
        if (customer is null)
            return;

        _editingId = customer.Id;
        EditTag = customer.Tag;
        EditDisplayName = customer.DisplayName;
        IsEditing = true;
        IsAdding = false;
        ClearFormError();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        _editingId = null;
        EditTag = "";
        EditDisplayName = "";
        ClearFormError();
    }

    [RelayCommand]
    private void SaveEdit()
    {
        if (_editingId is null)
            return;

        var result = Invoke(() => _catalog.Update(_editingId, EditTag, EditDisplayName));
        if (!ApplyEditResult(result))
            return;

        IsEditing = false;
        _editingId = null;
        StatusMessage = _localization.Get(L.StatusCustomerUpdated);
        Reload();
    }

    [RelayCommand]
    private void BeginRemove(string id)
    {
        var customer = _customers.FirstOrDefault(choice => Same(choice.Id, id));
        if (customer is null)
            return;

        _pendingRemovalId = customer.Id;
        PendingRemovalBody = string.Format(
            _localization.Get(L.CustomerRemoveBody),
            customer.Label);
        OnPropertyChanged(nameof(HasPendingRemoval));
    }

    [RelayCommand]
    private void CancelRemove()
    {
        _pendingRemovalId = null;
        PendingRemovalBody = "";
        OnPropertyChanged(nameof(HasPendingRemoval));
    }

    [RelayCommand]
    private void ConfirmRemove()
    {
        if (_pendingRemovalId is null)
            return;

        var result = Invoke(() => _catalog.Remove(_pendingRemovalId!));
        _pendingRemovalId = null;
        PendingRemovalBody = "";
        OnPropertyChanged(nameof(HasPendingRemoval));
        if (!ApplyEditResult(result))
            return;

        StatusMessage = _localization.Get(L.StatusCustomerRemoved);
        Reload();
    }

    public void Reload()
    {
        CustomerCatalog catalog;
        try
        {
            catalog = _catalog.Load();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            catalog = CustomerCatalog.Unreadable();
        }

        _customers.Clear();
        _customers.AddRange(catalog.Customers
            .OrderBy(customer => customer.Tag, StringComparer.OrdinalIgnoreCase)
            .Select(customer => new CustomerChoice(
                customer.Id,
                customer.Tag,
                customer.DisplayName,
                () => BeginEdit(customer.Id),
                () => BeginRemove(customer.Id))));

        CatalogWarning = catalog.Kind switch
        {
            CustomerCatalogKind.Unreadable => _localization.Get(L.CustomerCatalogUnreadable),
            CustomerCatalogKind.UnsupportedSchema => _localization.Get(L.CustomerCatalogUnsupported),
            _ => ""
        };

        var match = _selectedId is null
            ? null
            : _customers.FirstOrDefault(choice => Same(choice.Id, _selectedId));
        _suspendSelection = true;
        Choices.Clear();
        foreach (var customer in _customers)
            Choices.Add(customer);
        SelectedCustomer = match;
        _suspendSelection = false;

        if (_selectedId is not null && match is null)
        {
            _selectedId = null;
            PublishTag("");
        }
        else if (match is not null)
        {
            PublishTag(match.Tag);
        }

        ApplyFilter();
        RefreshText();
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        VisibleCustomers.Clear();
        foreach (var customer in _customers)
        {
            if (query.Length == 0
                || customer.Tag.Contains(query, StringComparison.OrdinalIgnoreCase)
                || customer.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                VisibleCustomers.Add(customer);
        }

        ShowEmpty = _customers.Count == 0;
        ShowNoMatches = _customers.Count > 0 && VisibleCustomers.Count == 0;
        ShowCustomerList = VisibleCustomers.Count > 0;
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowNoMatches));
        OnPropertyChanged(nameof(ShowCustomerList));
    }

    private void RefreshText()
    {
        SelectedTagText = SelectedCustomer is null
            ? _localization.Get(L.CustomerNoneSelected)
            : string.Format(_localization.Get(L.CustomerSelectedTag), SelectedCustomer.Tag);
        FormError = _formErrorKey.Length == 0 ? "" : _localization.Get(_formErrorKey);
        if (_pendingRemovalId is not null)
        {
            var customer = _customers.FirstOrDefault(choice => Same(choice.Id, _pendingRemovalId));
            PendingRemovalBody = customer is null
                ? ""
                : string.Format(_localization.Get(L.CustomerRemoveBody), customer.Label);
        }

        OnPropertyChanged(nameof(HasPendingRemoval));
    }

    private CustomerEditResult Invoke(Func<CustomerEditResult> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CustomerEditResult.Fail(CustomerEditError.CatalogUnavailable);
        }
    }

    private bool ApplyEditResult(CustomerEditResult result)
    {
        _formErrorKey = result.Error switch
        {
            CustomerEditError.None => "",
            CustomerEditError.Required => L.CustomerRequired,
            CustomerEditError.Invalid => L.CustomerInvalid,
            CustomerEditError.Duplicate => L.CustomerDuplicate,
            CustomerEditError.DisplayNameInvalid => L.CustomerDisplayNameInvalid,
            CustomerEditError.Missing => L.CustomerMissing,
            _ => L.StatusCustomerSaveFailed
        };
        if (result.Error == CustomerEditError.CatalogUnavailable)
            CatalogWarning = _localization.Get(L.CustomerCatalogUnreadable);

        RefreshText();
        return result.Error == CustomerEditError.None;
    }

    private void PublishTag(string tag)
    {
        if (string.Equals(_publishedTag, tag, StringComparison.Ordinal))
            return;

        _publishedTag = tag;
        _setTag(tag);
    }

    private void ClearFormError()
    {
        _formErrorKey = "";
        FormError = "";
    }

    private static bool Same(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}

public sealed class CustomerChoice
{
    public CustomerChoice(string id, string tag, string displayName, Action edit, Action remove)
    {
        Id = id;
        Tag = tag;
        DisplayName = displayName;
        EditCommand = new RelayCommand(edit);
        RemoveCommand = new RelayCommand(remove);
    }

    public string Id { get; }

    public string Tag { get; }

    public string DisplayName { get; }

    public string Label => DisplayName.Length == 0 ? Tag : Tag + " — " + DisplayName;

    public IRelayCommand EditCommand { get; }

    public IRelayCommand RemoveCommand { get; }

    public override string ToString() => Label;
}
