import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

class _TestBoardRepository implements BoardRepository {
  _TestBoardRepository({this.changeStatusError});

  final Exception? changeStatusError;
  final List<String> statusChanges = [];

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

  @override
  Future<void> changeStatus(String cardId, String newStatusId) async {
    statusChanges.add('$cardId->$newStatusId');
    final error = changeStatusError;
    if (error != null) throw error;
  }
}

class _FailingLoadRepository implements BoardRepository {
  @override
  Future<BoardData> loadBoard() => Future.error(Exception('network down'));

  @override
  Future<void> changeStatus(String cardId, String newStatusId) =>
      Future.value();
}

void main() {
  late _TestBoardRepository repository;
  late BoardViewModel viewModel;

  setUp(() async {
    repository = _TestBoardRepository();
    viewModel = BoardViewModel(repository);
    await viewModel.load();
  });

  test('load sorts statuses by order', () {
    expect(viewModel.statuses.map((s) => s.id), ['todo', 'done']);
  });

  test('load sets loadError when the repository throws', () async {
    final failingViewModel = BoardViewModel(_FailingLoadRepository());
    await failingViewModel.load();

    expect(failingViewModel.loadError, isNotNull);
    expect(failingViewModel.isLoading, isFalse);
  });

  test('moveCard changes the status of only the moved card', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(card, 'done');

    expect(viewModel.swimlanes[0].cards.single.statusId, 'done');
  });

  test('moveCard never moves a card into a different swimlane', () async {
    final cardInLaneA = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(cardInLaneA, 'done');

    expect(viewModel.swimlanes[0].cards.single.parentId, 'lane-a');
    expect(viewModel.swimlanes[1].cards.single.parentId, 'lane-b');
    expect(viewModel.swimlanes[1].cards.single.statusId, 'todo');
  });

  test('moveCard persists the change through the repository', () async {
    final card = viewModel.swimlanes[0].cards.single;

    await viewModel.moveCard(card, 'done');

    expect(repository.statusChanges, ['card-1->done']);
  });

  test(
    'moveCard rolls back the optimistic update when the repository call fails',
    () async {
      final failingRepository = _TestBoardRepository(
        changeStatusError: Exception('conflict'),
      );
      final failingViewModel = BoardViewModel(failingRepository);
      await failingViewModel.load();
      final card = failingViewModel.swimlanes[0].cards.single;

      await failingViewModel.moveCard(card, 'done');

      expect(failingViewModel.swimlanes[0].cards.single.statusId, 'todo');
      expect(failingViewModel.moveError, isNotNull);
    },
  );
}
