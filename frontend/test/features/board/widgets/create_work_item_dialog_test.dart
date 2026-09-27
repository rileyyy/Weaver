import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/create_work_item_result.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/widgets/create_work_item_dialog.dart';

const _lanes = [
  Swimlane(parentId: 'lane-a', title: 'Lane A', cards: []),
  Swimlane(parentId: 'lane-b', title: 'Lane B', cards: []),
];

void main() {
  late CreateWorkItemResult? result;
  late bool closed;

  Future<void> openDialog(
    WidgetTester tester, {
    List<Swimlane> swimlanes = _lanes,
  }) async {
    result = null;
    closed = false;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () async {
              result = await showCreateWorkItemDialog(
                context,
                scopeParentId: 'scope-1',
                swimlanes: swimlanes,
              );
              closed = true;
            },
            child: const Text('open'),
          ),
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
  }

  FilledButton createButton(WidgetTester tester) =>
      tester.widget(find.widgetWithText(FilledButton, 'Create'));

  testWidgets('Create stays disabled until the title has text', (tester) async {
    await openDialog(tester);
    expect(createButton(tester).onPressed, isNull);

    await tester.enterText(find.widgetWithText(TextField, 'Title'), '   ');
    await tester.pump();
    expect(createButton(tester).onPressed, isNull);

    await tester.enterText(find.widgetWithText(TextField, 'Title'), 'Task');
    await tester.pump();
    expect(createButton(tester).onPressed, isNotNull);
  });

  testWidgets('defaults to a card in the first swimlane', (tester) async {
    await openDialog(tester);

    await tester.enterText(find.widgetWithText(TextField, 'Title'), ' Task ');
    await tester.pump();
    await tester.tap(find.text('Create'));
    await tester.pumpAndSettle();

    expect(result?.title, 'Task');
    expect(result?.description, isNull);
    expect(result?.parentId, 'lane-a');
  });

  testWidgets('a new swimlane is created under the current scope', (
    tester,
  ) async {
    await openDialog(tester);

    await tester.enterText(find.widgetWithText(TextField, 'Title'), 'Epic');
    await tester.enterText(
      find.widgetWithText(TextField, 'Description'),
      'Details',
    );
    await tester.tap(find.text('New swimlane at this level'));
    await tester.pump();
    await tester.tap(find.text('Create'));
    await tester.pumpAndSettle();

    expect(result?.parentId, 'scope-1');
    expect(result?.description, 'Details');
  });

  testWidgets('with no swimlanes, only a new swimlane is possible', (
    tester,
  ) async {
    await openDialog(tester, swimlanes: const []);

    expect(find.text('Swimlane'), findsNothing);
    final cardOption = tester.widget<RadioListTile<bool>>(
      find.widgetWithText(RadioListTile<bool>, 'Card in an existing swimlane'),
    );
    expect(cardOption.enabled, isFalse);
  });

  testWidgets('Cancel closes without a result', (tester) async {
    await openDialog(tester);

    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    expect(closed, isTrue);
    expect(result, isNull);
  });
}
