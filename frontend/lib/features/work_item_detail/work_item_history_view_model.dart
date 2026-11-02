import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/work_item_detail/data/work_item_history_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

/// The detail dialog's History tab. Read-only: history is written by the
/// server as a side effect of every change, never edited from here.
@injectable
class WorkItemHistoryViewModel extends ViewModel {
  WorkItemHistoryViewModel(this._repository);

  final WorkItemHistoryRepository _repository;

  List<WorkItemHistoryEntry> _entries = const [];
  bool _isLoading = false;
  String? _loadError;

  /// Newest first, as the server sends them.
  List<WorkItemHistoryEntry> get entries => _entries;
  bool get isLoading => _isLoading;
  String? get loadError => _loadError;

  /// Called each time the tab is shown, so edits made on the Details tab
  /// since the last visit appear.
  Future<void> load(String workItemId) async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      _entries = await _repository.loadHistory(workItemId);
    } on ApiException {
      _entries = const [];
      _loadError = 'Could not load the history.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }
}
