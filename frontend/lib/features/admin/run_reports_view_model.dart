import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/run_report_repository.dart';
import 'package:weaver/features/admin/models/run_key.dart';
import 'package:weaver/features/admin/models/run_report.dart';
import 'package:weaver/features/admin/state/run_changes.dart';
import 'package:weaver/features/admin/state/run_undo.dart';

/// The Admin page's Runs tab: agent runs newest first, a page at a time, and
/// each run's changes, loaded the first time it's expanded, and undoing a
/// run. The server sizes the pages and says where the next one starts.
@injectable
class RunReportsViewModel extends ViewModel {
  RunReportsViewModel(this._repository);

  final RunReportRepository _repository;

  List<RunReport> _runs = const [];
  int? _nextOffset;
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  bool _isLoadingMore = false;
  String? _loadMoreError;
  int _loadGeneration = 0;
  final Map<RunKey, RunChanges> _changes = {};
  final Map<RunKey, RunUndo> _undos = {};

  List<RunReport> get runs => _runs;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;
  bool get hasMore => _nextOffset != null;
  bool get isLoadingMore => _isLoadingMore;
  String? get loadMoreError => _loadMoreError;

  /// Null until [run] is first expanded.
  RunChanges? changesFor(RunReport run) => _changes[run.key];

  /// Null until [run] is first undone.
  RunUndo? undoFor(RunReport run) => _undos[run.key];

  Future<void> load() async {
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final page = await _repository.list(offset: 0);
      if (generation != _loadGeneration) return;
      _runs = page.runs;
      _nextOffset = page.nextOffset;
      _changes.clear();
      _undos.clear();
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the runs. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  Future<void> loadMore() async {
    final offset = _nextOffset;
    if (offset == null || _isLoadingMore) return;
    final generation = _loadGeneration;
    _isLoadingMore = true;
    _loadMoreError = null;
    notifyIfActive();
    try {
      final page = await _repository.list(offset: offset);
      if (generation != _loadGeneration) return;
      _runs = [..._runs, ...page.runs];
      _nextOffset = page.nextOffset;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadMoreError = 'Could not load more runs. Try again.';
    } finally {
      _isLoadingMore = false;
      notifyIfActive();
    }
  }

  /// Does nothing while the changes are loading or once they have; tries
  /// again after a failure.
  Future<void> loadChanges(RunReport run) async {
    final current = _changes[run.key];
    if (current != null && current.error == null) return;
    _changes[run.key] = const RunChanges.loading();
    notifyIfActive();
    try {
      _changes[run.key] = RunChanges.loaded(await _repository.changes(run));
    } on ApiException {
      _changes[run.key] = const RunChanges.failed(
        'Could not load this run’s changes. Try again.',
      );
    }
    notifyIfActive();
  }

  /// Does nothing while an undo of [run] is under way. Repeating a finished
  /// one is safe: the server skips everything the first undo put back.
  Future<void> undo(RunReport run) async {
    if (_undos[run.key]?.isInProgress ?? false) return;
    _undos[run.key] = const RunUndo.inProgress();
    notifyIfActive();
    try {
      _undos[run.key] = RunUndo.done(await _repository.undo(run));
    } on ApiException {
      _undos[run.key] = const RunUndo.failed(
        'Could not undo this run. Nothing was changed; try again.',
      );
    }
    notifyIfActive();
  }
}
