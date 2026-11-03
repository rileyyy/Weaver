import 'package:flutter/foundation.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/features/admin/state/reordered.dart';
import 'package:weaver/shared/models/space.dart';

/// Loading and drag-to-reorder for an admin list of lookup values; the
/// subclasses add creating, editing and deleting their own kind. Each Space
/// owns its own values, so one Space's are listed at a time.
abstract class OrderedListViewModel<T> extends ViewModel {
  List<T> _items = const [];
  String? _spaceId;
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<T> get items => _items;

  /// The Space whose values are shown; null until one is chosen.
  String? get spaceId => _spaceId;

  /// Whether there's a list to show and add to: a Space must be chosen
  /// first.
  bool get hasList => _spaceId != null;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  @protected
  String idOf(T item);

  @protected
  Future<List<T>> fetch();

  @protected
  Future<List<T>> saveOrder(List<String> orderedIds);

  /// Plural noun for messages, e.g. "statuses".
  @protected
  String get pluralName;

  /// Shows [spaceId]'s values instead of the current Space's.
  Future<void> showSpace(String spaceId) {
    if (spaceId == _spaceId) return Future.value();
    _spaceId = spaceId;
    _items = const [];
    _hasLoaded = false;
    return load();
  }

  /// Keeps the chosen Space while it's among [spaces], and otherwise shows
  /// the first of them.
  Future<void> followSpaces(List<Space> spaces) {
    final keepsCurrent = spaces.any((space) => space.id == _spaceId);
    if (spaces.isEmpty || keepsCurrent) return Future.value();
    return showSpace(spaces.first.id);
  }

  Future<void> load() async {
    if (!hasList) return;
    final generation = ++_loadGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final items = await fetch();
      if (generation != _loadGeneration) return;
      _items = items;
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration) return;
      _loadError = 'Could not load the $pluralName. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  /// Moves an item as `ReorderableListView.onReorderItem` reports it, showing
  /// the new order straight away. Returns null on success; on failure the
  /// previous order comes back and the reason is returned for a snackbar.
  Future<String?> move(int oldIndex, int newIndex) async {
    final previous = _items;
    final moved = reordered(previous, oldIndex, newIndex);
    _items = moved;
    notifyIfActive();
    try {
      final saved = await saveOrder([for (final item in moved) idOf(item)]);
      if (identical(_items, moved)) {
        _items = saved;
        notifyIfActive();
      }
      return null;
    } on ApiException catch (e) {
      if (identical(_items, moved)) {
        _items = previous;
        notifyIfActive();
      }
      return adminErrorMessage(
        e,
        'Could not reorder the $pluralName. Try again.',
      );
    }
  }

  @protected
  void setItems(List<T> items) {
    _items = items;
    notifyIfActive();
  }

  /// Runs a change and applies its result. Returns null on success, or why
  /// it failed for the dialog or snackbar that asked for it. A result that
  /// arrives after another Space was chosen belongs to the old Space's list,
  /// which that Space's next load shows, so it isn't applied.
  @protected
  Future<String?> attempt<R>(
    Future<R> Function() change, {
    required void Function(R result) onSuccess,
    required String fallbackError,
  }) async {
    final spaceId = _spaceId;
    try {
      final result = await change();
      if (spaceId != _spaceId) return null;
      onSuccess(result);
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, fallbackError);
    }
  }
}
