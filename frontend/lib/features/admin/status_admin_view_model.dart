import 'package:injectable/injectable.dart';
import 'package:weaver/features/admin/data/status_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/features/admin/ordered_list_view_model.dart';

/// The Admin page's Statuses tab.
@injectable
class StatusAdminViewModel extends OrderedListViewModel<AdminStatus> {
  StatusAdminViewModel(this._repository);

  final StatusAdminRepository _repository;

  /// Every work item needs a status, so the last one can't go.
  bool get canDelete => items.length > 1;

  @override
  String idOf(AdminStatus item) => item.id;

  @override
  Future<List<AdminStatus>> fetch() => _repository.list();

  @override
  Future<List<AdminStatus>> saveOrder(List<String> orderedIds) =>
      _repository.reorder(orderedIds);

  @override
  String get pluralName => 'statuses';

  Future<String?> create(StatusDetails details) => attempt(
    () => _repository.create(details),
    onSuccess: (created) => setItems([...items, created]),
    fallbackError: 'Could not add the status. Try again.',
  );

  Future<String?> update(String id, StatusDetails details) => attempt(
    () => _repository.update(id, details),
    onSuccess: (updated) => setItems([
      for (final status in items) status.id == id ? updated : status,
    ]),
    fallbackError: 'Could not save the status. Try again.',
  );

  /// Moves the deleted status's work items to [replacementId].
  Future<String?> delete(String id, {required String replacementId}) => attempt(
    () => _repository.delete(id, replacementId: replacementId),
    onSuccess: (_) => setItems([
      for (final status in items)
        if (status.id != id) status,
    ]),
    fallbackError: 'Could not delete the status. Try again.',
  );
}
