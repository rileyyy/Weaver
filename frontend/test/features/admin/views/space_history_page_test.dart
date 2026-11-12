import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/space_history_view_model.dart';
import 'package:weaver/features/admin/views/space_history_page.dart';
import 'package:weaver/shared/models/space.dart';

import '../fakes/fake_space_admin_repository.dart';
import '../fakes/space_history_entries.dart';

void main() {
  const house = Space(id: 'home', name: 'House', doneHidingDays: 30);

  Future<void> pumpPage(
    WidgetTester tester,
    FakeSpaceAdminRepository repository,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: SpaceHistoryPage(
          space: house,
          viewModel: SpaceHistoryViewModel(repository, house.id),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  testWidgets('lists each change newest first, with who made it', (
    tester,
  ) async {
    await pumpPage(
      tester,
      FakeSpaceAdminRepository(
        const [house],
        history: {
          'home': [
            spaceHistoryEntry(3, 'doneHidingDays', oldValue: 14, newValue: 30),
            spaceHistoryEntry(2, 'name', oldValue: 'Home', newValue: 'House'),
            spaceHistoryEntry(1, 'created', newValue: 'Home'),
          ],
        },
      ),
    );

    final changes = tester
        .widgetList<ListTile>(find.byType(ListTile))
        .map((tile) => (tile.title! as Text).data)
        .toList();
    expect(find.text('History of House'), findsOneWidget);
    expect(changes, [
      'Done-hiding period: 14 days → 30 days',
      'Renamed "Home" → "House"',
      'Created "Home"',
    ]);
    expect(find.text('admin'), findsNWidgets(3));
  });

  testWidgets('says so when nothing is recorded', (tester) async {
    await pumpPage(tester, FakeSpaceAdminRepository(const [house]));

    expect(find.text('No changes recorded.'), findsOneWidget);
  });
}
