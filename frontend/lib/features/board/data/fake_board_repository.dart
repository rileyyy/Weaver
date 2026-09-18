import 'package:injectable/injectable.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Fixed, in-memory board data for the standalone prototype (Milestone 4).
/// No persistence, no API call — moves made through [BoardViewModel] only
/// live for the current app session.
@LazySingleton(as: BoardRepository)
class FakeBoardRepository implements BoardRepository {
  static const _statuses = [
    BoardStatus(id: 'todo', name: 'To Do', order: 0),
    BoardStatus(id: 'in-progress', name: 'In Progress', order: 1),
    BoardStatus(id: 'done', name: 'Done', order: 2),
  ];

  static final _swimlanes = [
    const Swimlane(
      parentId: 'epic-board',
      title: 'Swimlane board',
      cards: [
        WorkItemCard(
          id: 'wi-1',
          title: 'Design swimlane layout',
          parentId: 'epic-board',
          statusId: 'done',
        ),
        WorkItemCard(
          id: 'wi-2',
          title: 'Implement drag-and-drop',
          parentId: 'epic-board',
          statusId: 'in-progress',
        ),
        WorkItemCard(
          id: 'wi-3',
          title: 'Wire the board up to the API',
          parentId: 'epic-board',
          statusId: 'todo',
        ),
      ],
    ),
    const Swimlane(
      parentId: 'epic-auth',
      title: 'Authentication',
      cards: [
        WorkItemCard(
          id: 'wi-4',
          title: 'Login screen',
          parentId: 'epic-auth',
          statusId: 'todo',
        ),
        WorkItemCard(
          id: 'wi-5',
          title: 'Session storage',
          parentId: 'epic-auth',
          statusId: 'todo',
        ),
      ],
    ),
  ];

  @override
  Future<BoardData> loadBoard() =>
      Future.value(BoardData(statuses: _statuses, swimlanes: _swimlanes));
}
