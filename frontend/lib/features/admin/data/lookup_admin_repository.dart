import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

/// Priority and Type management. Each Space has its own; editing and
/// deleting one by id stays within its Space. Every write needs an admin;
/// the server answers 403 otherwise.
abstract class LookupAdminRepository {
  Future<List<LookupValue>> list(LookupKind kind, String spaceId);

  /// Appends a value to [spaceId]'s. [color] is required for kinds where
  /// [LookupKind.hasColor] and ignored for the others; the same goes for
  /// [update].
  Future<LookupValue> create(
    LookupKind kind,
    String spaceId,
    String name, {
    String? color,
  });

  Future<LookupValue> update(
    LookupKind kind,
    String id,
    String name, {
    String? color,
  });

  /// [orderedIds] lists every value of [spaceId] in its new order.
  Future<List<LookupValue>> reorder(
    LookupKind kind,
    String spaceId,
    List<String> orderedIds,
  );

  /// Work items using the deleted value move to [replacementId], a value of
  /// the same Space, or to none when it's null.
  Future<void> delete(LookupKind kind, String id, {String? replacementId});
}
