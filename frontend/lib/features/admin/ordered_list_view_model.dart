import 'package:flutter/foundation.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/features/admin/state/reordered.dart';

/// Loading and drag-to-reorder for an admin list of lookup values; the
/// subclasses add creating, editing and deleting their own kind.
abstract class OrderedListViewModel<T> extends ViewModel {
  List<T> _items = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  int _loadGeneration = 0;

  List<T> get items => _items;

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

  Future<void> load() async {
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
  /// it failed for the dialog or snackbar that asked for it.
  @protected
  Future<String?> attempt<R>(
    Future<R> Function() change, {
    required void Function(R result) onSuccess,
    required String fallbackError,
  }) async {
    try {
      final result = await change();
      onSuccess(result);
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, fallbackError);
    }
  }
}
