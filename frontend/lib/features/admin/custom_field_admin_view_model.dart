import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/admin/data/custom_field_admin_repository.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/features/admin/state/admin_error_message.dart';
import 'package:weaver/features/admin/state/reordered.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

/// One Type's custom fields on the Admin page: listing, adding, editing,
/// reordering and removing them. Each change returns null on success, or
/// why it failed for the dialog or snackbar that asked for it.
@injectable
class CustomFieldAdminViewModel extends ViewModel {
  CustomFieldAdminViewModel(this._repository, @factoryParam this.typeId);

  final CustomFieldAdminRepository _repository;
  final String typeId;

  List<CustomField> _items = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;

  List<CustomField> get items => _items;

  /// Only true for the first load; later refreshes keep showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  Future<void> load() async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      _items = await _repository.list(typeId);
      _hasLoaded = true;
    } on ApiException {
      _loadError = 'Could not load the fields. Check your connection.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  Future<String?> create(
    String name,
    CustomFieldKind kind,
    List<String> optionNames,
  ) => _attempt(
    () => _repository.create(typeId, name, kind, optionNames),
    onSuccess: (created) => _items = [..._items, created],
    fallbackError: 'Could not add the field. Try again.',
  );

  Future<String?> update(
    String id,
    String name,
    List<CustomFieldOptionDraft> options,
  ) => _attempt(
    () => _repository.update(id, name, options),
    onSuccess: (updated) =>
        _items = [for (final field in _items) field.id == id ? updated : field],
    fallbackError: 'Could not save the field. Try again.',
  );

  Future<String?> delete(String id) => _attempt(
    () => _repository.delete(id),
    onSuccess: (_) => _items = [
      for (final field in _items)
        if (field.id != id) field,
    ],
    fallbackError: 'Could not remove the field. Try again.',
  );

  /// Shows the new order straight away; on failure the previous order comes
  /// back and the reason is returned.
  Future<String?> move(int oldIndex, int newIndex) async {
    final previous = _items;
    final moved = reordered(previous, oldIndex, newIndex);
    _items = moved;
    notifyIfActive();
    try {
      final saved = await _repository.reorder(typeId, [
        for (final field in moved) field.id,
      ]);
      if (identical(_items, moved)) _items = saved;
      return null;
    } on ApiException catch (e) {
      if (identical(_items, moved)) _items = previous;
      return adminErrorMessage(e, 'Could not reorder the fields. Try again.');
    } finally {
      notifyIfActive();
    }
  }

  Future<String?> _attempt<R>(
    Future<R> Function() change, {
    required void Function(R result) onSuccess,
    required String fallbackError,
  }) async {
    try {
      onSuccess(await change());
      notifyIfActive();
      return null;
    } on ApiException catch (e) {
      return adminErrorMessage(e, fallbackError);
    }
  }
}
