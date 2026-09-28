import 'dart:async';

import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/status_admin_repository.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/status_details.dart';

AdminStatus adminStatus(String id, {String? name, int order = 0}) =>
    AdminStatus(
      id: id,
      name: name ?? id,
      order: order,
      category: StatusCategory.toDo,
      color: '#1E88E5',
    );

class FakeStatusAdminRepository implements StatusAdminRepository {
  FakeStatusAdminRepository(this.statuses);

  List<AdminStatus> statuses;
  ApiException? error;
  final List<String> calls = [];

  /// When set, [reorder] waits on it, so a test can see the optimistic order.
  Completer<void>? reorderGate;

  Future<void> _fail() async {
    final error = this.error;
    if (error != null) throw error;
  }

  @override
  Future<List<AdminStatus>> list() async {
    await _fail();
    return statuses;
  }

  @override
  Future<AdminStatus> create(StatusDetails details) async {
    calls.add('create ${details.name}');
    await _fail();
    return AdminStatus(
      id: 'new',
      name: details.name,
      order: statuses.length,
      category: details.category,
      color: details.color,
    );
  }

  @override
  Future<AdminStatus> update(String id, StatusDetails details) async {
    calls.add('update $id');
    await _fail();
    return AdminStatus(
      id: id,
      name: details.name,
      order: 0,
      category: details.category,
      color: details.color,
    );
  }

  @override
  Future<List<AdminStatus>> reorder(List<String> orderedIds) async {
    calls.add('reorder ${orderedIds.join(',')}');
    await reorderGate?.future;
    await _fail();
    return [
      for (final (index, id) in orderedIds.indexed)
        adminStatus(id, order: index),
    ];
  }

  @override
  Future<void> delete(String id, {required String replacementId}) async {
    calls.add('delete $id -> $replacementId');
    await _fail();
  }
}
