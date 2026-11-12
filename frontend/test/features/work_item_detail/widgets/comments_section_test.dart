import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/widgets/comments_section.dart';

void main() {
  late List<String> posted;

  setUp(() => posted = []);

  Widget section() => MaterialApp(
    home: Scaffold(
      body: CommentsSection(
        comments: const [],
        mentionHandles: const ['claude-research', 'llama'],
        isOwnComment: (_) => true,
        onAdd: (body) async {
          posted.add(body);
          return true;
        },
        onEdit: (_, _) async => true,
        onDelete: (_) async => true,
      ),
    ),
  );

  Finder commentBox() => find.byType(TextField);

  testWidgets('typing @ suggests the agents that can be mentioned', (
    tester,
  ) async {
    await tester.pumpWidget(section());

    await tester.enterText(commentBox(), 'Hey @');
    await tester.pump();

    expect(find.text('@claude-research'), findsOneWidget);
    expect(find.text('@llama'), findsOneWidget);
  });

  testWidgets('suggestions narrow as the handle is typed', (tester) async {
    await tester.pumpWidget(section());

    await tester.enterText(commentBox(), '@cl');
    await tester.pump();

    expect(find.text('@claude-research'), findsOneWidget);
    expect(find.text('@llama'), findsNothing);
  });

  testWidgets('picking a suggestion completes the handle', (tester) async {
    await tester.pumpWidget(section());

    await tester.enterText(commentBox(), '@cl');
    await tester.pump();
    await tester.tap(find.text('@claude-research'));
    await tester.pump();
    await tester.enterText(commentBox(), '${_text(tester)}find quotes');
    await tester.tap(find.text('Post'));
    await tester.pump();

    expect(posted, ['@claude-research find quotes']);
    expect(find.text('@llama'), findsNothing);
  });

  testWidgets('no suggestions show without an @ being typed', (tester) async {
    await tester.pumpWidget(section());

    await tester.enterText(commentBox(), 'riley@cl');
    await tester.pump();

    expect(find.text('@claude-research'), findsNothing);
  });
}

String _text(WidgetTester tester) =>
    tester.widget<TextField>(find.byType(TextField)).controller!.text;
