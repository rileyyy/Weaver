import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Finds the work items [text] matches; each picker decides which (one
/// Space for a parent, every Space for a link).
typedef WorkItemSearch = Future<List<WorkItem>> Function(String text);

/// The state behind a work-item picker: the latest search's results.
class WorkItemPickerViewModel extends ViewModel {
  WorkItemPickerViewModel(this._search);

  final WorkItemSearch _search;

  List<WorkItem> _results = const [];
  bool _isSearching = false;
  String? _error;

  // Searches run as you type, so an older one can answer after a newer one.
  int _latestSearch = 0;

  List<WorkItem> get results => _results;
  bool get isSearching => _isSearching;
  String? get error => _error;

  Future<void> search(String text) async {
    final searchNumber = ++_latestSearch;
    _isSearching = true;
    notifyIfActive();

    List<WorkItem> results;
    String? error;
    try {
      results = await _search(text);
    } on ApiException {
      results = const [];
      error = 'Could not search. Check your connection and try again.';
    }
    if (searchNumber != _latestSearch) return;
    _results = results;
    _error = error;
    _isSearching = false;
    notifyIfActive();
  }
}
