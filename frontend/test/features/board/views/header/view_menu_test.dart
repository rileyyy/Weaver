import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/views/header/widgets/view_menu.dart';
import 'package:weaver/features/board/views/header/widgets/view_menu_action.dart';
import 'package:weaver/features/board/views/header/widgets/view_name_dialog.dart';

const _overview = SavedView(
  id: 'overview',
  builtIn: true,
  definition: ViewDefinition(name: 'Overview'),
);
const _village = SavedView(
  id: 'village',
  definition: ViewDefinition(name: 'Village only'),
);

void main() {
  final opened = <String>[];
  final actions = <ViewMenuAction>[];

  setUp(() {
    opened.clear();
    actions.clear();
  });

  Future<void> pumpMenu(
    WidgetTester tester, {
    SavedView current = _overview,
    String defaultViewId = 'overview',
  }) => tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: ViewMenu(
          views: const [_overview, _village],
          current: current,
          defaultViewId: defaultViewId,
          onOpen: (view) => opened.add(view.id),
          onAction: actions.add,
        ),
      ),
    ),
  );

  Future<void> openMenu(WidgetTester tester) async {
    await tester.tap(find.byType(ViewMenu));
    await tester.pumpAndSettle();
  }

  testWidgets('shows the open View and switches to another', (tester) async {
    await pumpMenu(tester);
    expect(find.text('Overview'), findsOneWidget);

    await openMenu(tester);
    await tester.tap(find.text('Village only'));
    await tester.pumpAndSettle();

    expect(opened, ['village']);
  });

  testWidgets('stars the default View', (tester) async {
    await pumpMenu(tester, defaultViewId: 'village');
    await openMenu(tester);

    final villageRow = find.ancestor(
      of: find.text('Village only'),
      matching: find.byType(ListTile),
    );
    expect(
      find.descendant(of: villageRow, matching: find.byIcon(Icons.star)),
      findsOneWidget,
    );
  });

  testWidgets('offers saving as a new View', (tester) async {
    await pumpMenu(tester);
    await openMenu(tester);

    await tester.tap(find.text('Save as new View…'));
    await tester.pumpAndSettle();

    expect(actions, [ViewMenuAction.saveAs]);
  });

  testWidgets("Overview can't be changed, renamed or deleted", (tester) async {
    await pumpMenu(tester);
    await openMenu(tester);

    expect(find.textContaining('Save changes'), findsNothing);
    expect(find.text('Rename…'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('a saved View can be changed, renamed, deleted and made the '
      'default', (tester) async {
    await pumpMenu(tester, current: _village);
    await openMenu(tester);

    expect(find.text('Save changes to "Village only"'), findsOneWidget);
    expect(find.text('Rename…'), findsOneWidget);
    expect(find.text('Delete'), findsOneWidget);
    await tester.tap(find.text('Make my default'));
    await tester.pumpAndSettle();
    expect(actions, [ViewMenuAction.makeDefault]);
  });

  group('the name dialog', () {
    late String? result;

    Future<void> openDialog(WidgetTester tester) async {
      result = 'not closed';
      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) => TextButton(
              onPressed: () async => result = await showViewNameDialog(
                context,
                title: 'Save as new View',
                confirmLabel: 'Save',
              ),
              child: const Text('open'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
    }

    testWidgets('returns the trimmed name', (tester) async {
      await openDialog(tester);

      await tester.enterText(find.byType(TextField), '  Gates  ');
      await tester.pump();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(result, 'Gates');
    });

    testWidgets("can't save a blank name", (tester) async {
      await openDialog(tester);

      await tester.enterText(find.byType(TextField), '   ');
      await tester.pump();

      final save = tester.widget<FilledButton>(find.byType(FilledButton));
      expect(save.onPressed, isNull);
    });
  });
}
