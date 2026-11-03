import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/lookup_admin_repository.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';

class FakeLookupAdminRepository implements LookupAdminRepository {
  FakeLookupAdminRepository(this.values);

  List<LookupValue> values;
  ApiException? error;
  final List<String> calls = [];

  static String _in(String? spaceId) => spaceId == null ? '' : ' in $spaceId';

  Future<void> _fail() async {
    final error = this.error;
    if (error != null) throw error;
  }

  @override
  Future<List<LookupValue>> list(LookupKind kind, {String? spaceId}) async {
    calls.add('list ${kind.name}${_in(spaceId)}');
    await _fail();
    return values;
  }

  @override
  Future<LookupValue> create(
    LookupKind kind,
    String name, {
    String? color,
    String? spaceId,
  }) async {
    calls.add('create ${kind.name} $name $color${_in(spaceId)}');
    await _fail();
    return LookupValue(
      id: 'new',
      name: name,
      order: values.length,
      color: color,
    );
  }

  @override
  Future<LookupValue> update(
    LookupKind kind,
    String id,
    String name, {
    String? color,
  }) async {
    calls.add('update ${kind.name} $id $name $color');
    await _fail();
    return LookupValue(id: id, name: name, order: 0, color: color);
  }

  @override
  Future<List<LookupValue>> reorder(
    LookupKind kind,
    List<String> orderedIds, {
    String? spaceId,
  }) async {
    calls.add('reorder ${kind.name} ${orderedIds.join(',')}${_in(spaceId)}');
    await _fail();
    return [
      for (final (index, id) in orderedIds.indexed)
        LookupValue(id: id, name: id, order: index),
    ];
  }

  @override
  Future<void> delete(
    LookupKind kind,
    String id, {
    String? replacementId,
  }) async {
    calls.add('delete ${kind.name} $id -> $replacementId');
    await _fail();
  }
}
