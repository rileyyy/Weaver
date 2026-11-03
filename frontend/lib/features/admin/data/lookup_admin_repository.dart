import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

/// Priority and type management. Every write needs an admin; the server
/// answers 403 otherwise.
///
/// [spaceId] is required for kinds each Space owns its own values of
/// ([LookupKind.ownedBySpace]) and must be left out for the others.
abstract class LookupAdminRepository {
  Future<List<LookupValue>> list(LookupKind kind, {String? spaceId});

  /// [color] is required for kinds where [LookupKind.hasColor] and ignored
  /// for the others; the same goes for [update].
  Future<LookupValue> create(
    LookupKind kind,
    String name, {
    String? color,
    String? spaceId,
  });

  Future<LookupValue> update(
    LookupKind kind,
    String id,
    String name, {
    String? color,
  });

  /// [orderedIds] lists every value (of [spaceId]) in its new order.
  Future<List<LookupValue>> reorder(
    LookupKind kind,
    List<String> orderedIds, {
    String? spaceId,
  });

  /// Work items using the deleted value move to [replacementId], or to none
  /// when it's null.
  Future<void> delete(LookupKind kind, String id, {String? replacementId});
}
