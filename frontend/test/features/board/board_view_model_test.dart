import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

class _TestBoardRepository implements BoardRepository {
  @override
  Future<BoardData> loadBoard() => Future.value(
        const BoardData(
          statuses: [
            BoardStatus(id: 'done', name: 'Done', order: 1),
            BoardStatus(id: 'todo', name: 'To Do', order: 0),
          ],
          swimlanes: [
            Swimlane(
              parentId: 'lane-a',
              title: 'Lane A',
              cards: [
                WorkItemCard(
                  id: 'card-1',
                  title: 'Card 1',
                  parentId: 'lane-a',
                  statusId: 'todo',
                ),
              ],
            ),
            Swimlane(
              parentId: 'lane-b',
              title: 'Lane B',
              cards: [
                WorkItemCard(
                  id: 'card-2',
                  title: 'Card 2',
                  parentId: 'lane-b',
                  statusId: 'todo',
                ),
              ],
            ),
          ],
        ),
      );
}

void main() {
  late BoardViewModel viewModel;

  setUp(() async {
    viewModel = BoardViewModel(_TestBoardRepository());
    await viewModel.load();
  });

  test('load sorts statuses by order', () {
    expect(viewModel.statuses.map((s) => s.id), ['todo', 'done']);
  });

  test('moveCard changes the status of only the moved card', () {
    final card = viewModel.swimlanes[0].cards.single;

    viewModel.moveCard(card, 'done');

    expect(viewModel.swimlanes[0].cards.single.statusId, 'done');
  });

  test('moveCard never moves a card into a different swimlane', () {
    final cardInLaneA = viewModel.swimlanes[0].cards.single;

    viewModel.moveCard(cardInLaneA, 'done');

    expect(viewModel.swimlanes[0].cards.single.parentId, 'lane-a');
    expect(viewModel.swimlanes[1].cards.single.parentId, 'lane-b');
    expect(viewModel.swimlanes[1].cards.single.statusId, 'todo');
  });
}
