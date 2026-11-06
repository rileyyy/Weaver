import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/trash_repository.dart';
import 'package:weaver/features/board/models/trashed_work_item.dart';
import 'package:weaver/shared/models/user_kind.dart';

TrashedWorkItem trashed({
  String workItemId = 'item-1',
  int number = 1,
  String title = 'Old shed',
  String? spaceName = 'Home',
  DateTime? trashedAt,
  int itemCount = 1,
  String? deletedByName = 'alice',
  UserKind? deletedByKind = UserKind.human,
  String? deletedByActorLabel,
}) => TrashedWorkItem(
  workItemId: workItemId,
  number: number,
  title: title,
  spaceName: spaceName,
  trashedAt: trashedAt ?? DateTime(2026, 11, 4, 9),
  itemCount: itemCount,
  deletedByName: deletedByName,
  deletedByKind: deletedByKind,
  deletedByActorLabel: deletedByActorLabel,
);

/// Holds the Trash in memory. Set [error] to make every call fail with it.
class FakeTrashRepository implements TrashRepository {
  FakeTrashRepository([Iterable<TrashedWorkItem> initial = const []])
    : items = [...initial];

  final List<TrashedWorkItem> items;
  ApiException? error;
  final List<String> restoreCalls = [];
  final List<String> purgeCalls = [];
  int emptyCalls = 0;

  @override
  Future<List<TrashedWorkItem>> list() async {
    if (error case final e?) throw e;
    return [...items];
  }

  @override
  Future<void> restore(String workItemId) async {
    restoreCalls.add(workItemId);
    if (error case final e?) throw e;
    items.removeWhere((i) => i.workItemId == workItemId);
  }

  @override
  Future<void> purge(String workItemId) async {
    purgeCalls.add(workItemId);
    if (error case final e?) throw e;
    items.removeWhere((i) => i.workItemId == workItemId);
  }

  @override
  Future<void> empty() async {
    emptyCalls++;
    if (error case final e?) throw e;
    items.clear();
  }
}
