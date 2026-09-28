import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_details.dart';

/// Status management. Every write needs an admin; the server answers 403
/// otherwise.
abstract class StatusAdminRepository {
  Future<List<AdminStatus>> list();

  Future<AdminStatus> create(StatusDetails details);

  Future<AdminStatus> update(String id, StatusDetails details);

  /// [orderedIds] lists every status in its new order.
  Future<List<AdminStatus>> reorder(List<String> orderedIds);

  /// Work items in the deleted status move to [replacementId].
  Future<void> delete(String id, {required String replacementId});
}
