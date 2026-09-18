import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Stands in for the real, network-backed [BoardRepository] so this smoke
/// test never makes an HTTP call.
class _StubBoardRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() => Future.value();

  @override
  Future<BoardData> loadBoard(String? scopeItemId) => Future.value(
    const BoardData(
      statuses: [BoardStatus(id: 'todo', name: 'To Do', order: 0)],
      swimlanes: [
        Swimlane(
          parentId: 'epic-board',
          title: 'Swimlane board',
          cards: [
            WorkItemCard(
              id: 'wi-1',
              title: 'Design swimlane layout',
              parentId: 'epic-board',
              statusId: 'todo',
            ),
          ],
        ),
      ],
    ),
  );

  @override
  Future<void> changeStatus(String cardId, String newStatusId) =>
      Future.value();

  @override
  Future<void> reparentItem(String itemId, String newParentId) =>
      Future.value();
}

void main() {
  setUp(() async {
    await getIt.reset();
    configureDependencies();
    getIt
      ..unregister<BoardRepository>()
      ..registerLazySingleton<BoardRepository>(_StubBoardRepository.new);
  });

  testWidgets('renders the board with its swimlanes and columns', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();

    expect(find.text('Weaver'), findsOneWidget);
    expect(find.text('To Do'), findsOneWidget);
    expect(find.text('Swimlane board'), findsOneWidget);
    expect(find.text('Design swimlane layout'), findsOneWidget);
  });
}
