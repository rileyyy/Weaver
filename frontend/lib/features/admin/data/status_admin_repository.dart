import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_details.dart';

/// Status management. Each Space has its own statuses; editing and deleting
/// one by id stays within its Space. Every write needs an admin; the server
/// answers 403 otherwise.
abstract class StatusAdminRepository {
  Future<List<AdminStatus>> list(String spaceId);

  /// Appends a status to [spaceId]'s.
  Future<AdminStatus> create(String spaceId, StatusDetails details);

  Future<AdminStatus> update(String id, StatusDetails details);

  /// [orderedIds] lists every status of [spaceId] in its new order.
  Future<List<AdminStatus>> reorder(String spaceId, List<String> orderedIds);

  /// Work items in the deleted status move to [replacementId], a status of
  /// the same Space.
  Future<void> delete(String id, {required String replacementId});
}
