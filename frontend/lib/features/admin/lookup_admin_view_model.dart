import 'package:injectable/injectable.dart';
import 'package:weaver/features/admin/data/lookup_admin_repository.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/ordered_list_view_model.dart';

/// The Admin page's Priorities or Layers tab, depending on [kind].
@injectable
class LookupAdminViewModel extends OrderedListViewModel<LookupValue> {
  LookupAdminViewModel(this._repository, @factoryParam this.kind);

  final LookupAdminRepository _repository;
  final LookupKind kind;

  @override
  String idOf(LookupValue item) => item.id;

  @override
  Future<List<LookupValue>> fetch() => _repository.list(kind);

  @override
  Future<List<LookupValue>> saveOrder(List<String> orderedIds) =>
      _repository.reorder(kind, orderedIds);

  @override
  String get pluralName => kind.name;

  Future<String?> create(String name) => attempt(
    () => _repository.create(kind, name),
    onSuccess: (created) => setItems([...items, created]),
    fallbackError: 'Could not add the ${kind.singular}. Try again.',
  );

  Future<String?> rename(String id, String name) => attempt(
    () => _repository.rename(kind, id, name),
    onSuccess: (renamed) =>
        setItems([for (final value in items) value.id == id ? renamed : value]),
    fallbackError: 'Could not rename the ${kind.singular}. Try again.',
  );

  /// Moves the deleted value's work items to [replacementId], or leaves them
  /// with none when it's null.
  Future<String?> delete(String id, {String? replacementId}) => attempt(
    () => _repository.delete(kind, id, replacementId: replacementId),
    onSuccess: (_) => setItems([
      for (final value in items)
        if (value.id != id) value,
    ]),
    fallbackError: 'Could not delete the ${kind.singular}. Try again.',
  );
}
