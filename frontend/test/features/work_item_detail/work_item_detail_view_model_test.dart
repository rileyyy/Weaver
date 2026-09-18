import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart';
import 'package:weaver/features/work_item_detail/models/work_item_detail.dart';
import 'package:weaver/features/work_item_detail/models/work_item_layer.dart';
import 'package:weaver/features/work_item_detail/models/work_item_priority.dart';
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart';

WorkItemDetail _item({
  String id = 'item-1',
  String statusId = 'todo',
  String? layerId,
  WorkItemPriority priority = WorkItemPriority.medium,
  String? assignedToUserId,
}) => WorkItemDetail(
  id: id,
  parentId: null,
  title: 'A task',
  description: null,
  statusId: statusId,
  layerId: layerId,
  priority: priority,
  assignedToUserId: assignedToUserId,
  startDate: null,
  endDate: null,
  createdAtUtc: DateTime.utc(2026, 1, 1),
  updatedAtUtc: DateTime.utc(2026, 1, 1),
);

class _FakeRepository implements WorkItemDetailRepository {
  _FakeRepository({this.getItemError, this.saveError});

  final Exception? getItemError;
  final Exception? saveError;
  WorkItemDetail item = _item();
  final List<String> updateDetailsCalls = [];
  final List<String?> assignCalls = [];
  final List<String> rescheduleCalls = [];

  @override
  Future<WorkItemDetail> getItem(String id) async {
    final error = getItemError;
    if (error != null) throw error;
    return item;
  }

  @override
  Future<List<BoardStatus>> loadStatuses() async => const [
    BoardStatus(id: 'todo', name: 'To Do', order: 0),
    BoardStatus(id: 'done', name: 'Done', order: 1),
  ];

  @override
  Future<List<WorkItemLayer>> loadLayers() async =>
      const [WorkItemLayer(id: 'layer-task', name: 'Task', order: 0)];

  @override
  Future<List<AuthUser>> loadUsers() async =>
      const [AuthUser(id: 'user-1', username: 'alice', kind: UserKind.human)];

  @override
  Future<WorkItemDetail> updateDetails(
    String id, {
    required String title,
    required String? description,
    required String? layerId,
    required WorkItemPriority priority,
  }) async {
    updateDetailsCalls.add(id);
    final error = saveError;
    if (error != null) throw error;
    return item = _item(id: id, statusId: item.statusId, layerId: layerId, priority: priority);
  }

  @override
  Future<WorkItemDetail> assign(String id, String? userId) async {
    assignCalls.add(userId);
    final error = saveError;
    if (error != null) throw error;
    return item = _item(id: id, statusId: item.statusId, assignedToUserId: userId);
  }

  @override
  Future<WorkItemDetail> reschedule(String id, DateTime? startDate, DateTime? endDate) async {
    rescheduleCalls.add(id);
    final error = saveError;
    if (error != null) throw error;
    return item;
  }
}

void main() {
  test('load populates item, statuses, layers, and users', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.isLoading, isFalse);
    expect(viewModel.loadError, isNull);
    expect(viewModel.item!.id, 'item-1');
    expect(viewModel.statuses, hasLength(2));
    expect(viewModel.layers, hasLength(1));
    expect(viewModel.users, hasLength(1));
  });

  test('load sets loadError when the repository throws', () async {
    final repository = _FakeRepository(getItemError: Exception('network down'));
    final viewModel = WorkItemDetailViewModel(repository);

    await viewModel.load('item-1');

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.isLoading, isFalse);
  });

  test('statusName resolves the current status from the loaded list', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    expect(viewModel.statusName, 'To Do');
  });

  test('saveDetails updates the item and returns true on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      layerId: 'layer-task',
      priority: WorkItemPriority.high,
    );

    expect(ok, isTrue);
    expect(viewModel.item!.layerId, 'layer-task');
    expect(viewModel.item!.priority, WorkItemPriority.high);
    expect(viewModel.saveError, isNull);
  });

  test('saveDetails returns false and sets saveError on failure', () async {
    final repository = _FakeRepository(saveError: Exception('conflict'));
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveDetails(
      title: 'New title',
      description: null,
      layerId: null,
      priority: WorkItemPriority.medium,
    );

    expect(ok, isFalse);
    expect(viewModel.saveError, isNotNull);
  });

  test('saveAssignee updates the assignee on success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveAssignee('user-1');

    expect(ok, isTrue);
    expect(viewModel.item!.assignedToUserId, 'user-1');
    expect(repository.assignCalls, ['user-1']);
  });

  test('saveSchedule delegates to the repository and returns success', () async {
    final repository = _FakeRepository();
    final viewModel = WorkItemDetailViewModel(repository);
    await viewModel.load('item-1');

    final ok = await viewModel.saveSchedule(DateTime(2026, 2, 1), null);

    expect(ok, isTrue);
    expect(repository.rescheduleCalls, ['item-1']);
  });
}
