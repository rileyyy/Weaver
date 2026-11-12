import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/space_admin_repository.dart';
import 'package:weaver/features/admin/models/space_history_entry.dart';

/// One Space's history, on the page the Spaces tab opens for it. Read-only:
/// the server appends an entry with each change.
@injectable
class SpaceHistoryViewModel extends ViewModel {
  SpaceHistoryViewModel(this._repository, @factoryParam this.spaceId);

  final SpaceAdminRepository _repository;
  final String spaceId;

  List<SpaceHistoryEntry> _entries = const [];
  bool _isLoading = false;
  String? _loadError;
  int _loadGeneration = 0;

  /// Newest first.
  List<SpaceHistoryEntry> get entries => _entries;
  bool get isLoading => _isLoading;
  String? get loadError => _loadError;

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final entries = await _repository.history(spaceId);
      if (generation != _loadGeneration) return;
      _entries = entries;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the history. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }
}
