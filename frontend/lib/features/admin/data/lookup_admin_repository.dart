import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

/// Priority and layer management. Every write needs an admin; the server
/// answers 403 otherwise.
abstract class LookupAdminRepository {
  Future<List<LookupValue>> list(LookupKind kind);

  Future<LookupValue> create(LookupKind kind, String name);

  Future<LookupValue> rename(LookupKind kind, String id, String name);

  /// [orderedIds] lists every value in its new order.
  Future<List<LookupValue>> reorder(LookupKind kind, List<String> orderedIds);

  /// Work items using the deleted value move to [replacementId], or to none
  /// when it's null.
  Future<void> delete(LookupKind kind, String id, {String? replacementId});
}
