import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_url_link.dart';
import 'package:weaver/features/work_item_detail/widgets/url_links_section.dart';

Future<void> _pump(
  WidgetTester tester, {
  List<WorkItemUrlLink> links = const [],
  Future<bool> Function(String url, String label)? onAdd,
  ValueChanged<String>? onRemove,
  List<Uri>? opened,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: UrlLinksSection(
        links: links,
        onAdd: onAdd ?? (_, _) async => true,
        onRemove: onRemove ?? (_) {},
        openLink: (uri) async => opened?.add(uri),
      ),
    ),
  ),
);

Finder _field(String hint) => find.widgetWithText(TextField, hint);

void main() {
  testWidgets('shows the label with the URL under it, or just the URL', (
    tester,
  ) async {
    await _pump(
      tester,
      links: const [
        WorkItemUrlLink(
          id: 'u1',
          url: 'https://example.com/pr/7',
          label: 'PR 7',
        ),
        WorkItemUrlLink(id: 'u2', url: 'https://example.com/quote'),
      ],
    );

    expect(find.text('PR 7'), findsOneWidget);
    expect(find.text('https://example.com/pr/7'), findsOneWidget);
    expect(find.text('https://example.com/quote'), findsOneWidget);
  });

  testWidgets('tapping a web link opens it', (tester) async {
    final opened = <Uri>[];
    await _pump(
      tester,
      links: const [
        WorkItemUrlLink(id: 'u1', url: 'https://example.com/pr/7', label: 'PR'),
      ],
      opened: opened,
    );

    await tester.tap(find.text('PR'));

    expect(opened, [Uri.parse('https://example.com/pr/7')]);
  });

  testWidgets('removing a web link passes its id', (tester) async {
    final removed = <String>[];
    await _pump(
      tester,
      links: const [WorkItemUrlLink(id: 'u1', url: 'https://example.com')],
      onRemove: removed.add,
    );

    await tester.tap(find.byTooltip('Remove web link'));

    expect(removed, ['u1']);
  });

  testWidgets('adding sends the URL and label, then clears them', (
    tester,
  ) async {
    final added = <(String, String)>[];
    await _pump(
      tester,
      onAdd: (url, label) async {
        added.add((url, label));
        return true;
      },
    );

    await tester.enterText(_field('https://…'), 'https://example.com');
    await tester.enterText(_field('Label (optional)'), 'Quote');
    await tester.tap(find.widgetWithText(TextButton, 'Add web link'));
    await tester.pumpAndSettle();

    expect(added, [('https://example.com', 'Quote')]);
    expect(find.text('https://example.com'), findsNothing);
    expect(find.text('Quote'), findsNothing);
  });

  testWidgets('a refused web link keeps what was typed', (tester) async {
    await _pump(tester, onAdd: (_, _) async => false);

    await tester.enterText(_field('https://…'), 'example.com');
    await tester.tap(find.widgetWithText(TextButton, 'Add web link'));
    await tester.pumpAndSettle();

    expect(find.text('example.com'), findsOneWidget);
  });

  testWidgets('a blank URL is not sent', (tester) async {
    var calls = 0;
    await _pump(
      tester,
      onAdd: (_, _) async {
        calls++;
        return true;
      },
    );

    await tester.tap(find.widgetWithText(TextButton, 'Add web link'));
    await tester.pumpAndSettle();

    expect(calls, 0);
  });
}
