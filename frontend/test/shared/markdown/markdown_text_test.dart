import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/markdown/markdown_text.dart';
import 'package:weaver/shared/navigation/external_link_opener.dart';

Future<void> _show(
  WidgetTester tester,
  String data, {
  ValueChanged<int>? onToggleTask,
  LinkOpener? openLink,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: MarkdownText(
        data,
        onToggleTask: onToggleTask,
        openLink: openLink ?? (_) async {},
      ),
    ),
  ),
);

List<Checkbox> _checkboxes(WidgetTester tester) =>
    tester.widgetList<Checkbox>(find.byType(Checkbox)).toList();

void main() {
  testWidgets('renders Markdown formatting rather than the raw syntax', (
    tester,
  ) async {
    await _show(tester, '# Groceries\n\nBuy **fresh** bread');

    expect(find.text('Groceries', findRichText: true), findsOneWidget);
    expect(find.textContaining('**', findRichText: true), findsNothing);
    expect(find.textContaining('#', findRichText: true), findsNothing);
  });

  testWidgets('shows raw HTML as text instead of rendering it', (tester) async {
    await _show(tester, 'Hello <b>world</b>');

    expect(
      find.textContaining('<b>world</b>', findRichText: true),
      findsOneWidget,
    );
  });

  testWidgets('shows an image as its alt text without loading it', (
    tester,
  ) async {
    await _show(tester, '![floor plan](https://example.com/plan.png)');

    expect(find.byType(Image), findsNothing);
    expect(find.textContaining('floor plan', findRichText: true), findsWidgets);
  });

  group('links', () {
    Future<List<Uri>> tapLink(WidgetTester tester, String href) async {
      final opened = <Uri>[];
      await _show(
        tester,
        'See [the site]($href) now',
        openLink: (uri) async => opened.add(uri),
      );
      await tester.tapOnText(find.textRange.ofSubstring('the site'));
      return opened;
    }

    testWidgets('opens a web link', (tester) async {
      expect(await tapLink(tester, 'https://example.com/a'), [
        Uri.parse('https://example.com/a'),
      ]);
    });

    testWidgets('opens a mail link', (tester) async {
      expect(await tapLink(tester, 'mailto:a@example.com'), [
        Uri.parse('mailto:a@example.com'),
      ]);
    });

    testWidgets('ignores a link with an unsafe or missing scheme', (
      tester,
    ) async {
      expect(await tapLink(tester, 'javascript:alert(1)'), isEmpty);
      expect(await tapLink(tester, 'file:///etc/passwd'), isEmpty);
      expect(await tapLink(tester, 'relative/path'), isEmpty);
    });
  });

  group('task lists', () {
    testWidgets('draws a checkbox per task line with its ticked state', (
      tester,
    ) async {
      await _show(tester, '- [ ] milk\n- [x] eggs\n- [X] bread');

      expect(_checkboxes(tester).map((c) => c.value), [false, true, true]);
    });

    testWidgets('ticking reports the task in document order', (tester) async {
      final toggled = <int>[];
      await _show(
        tester,
        '- [ ] kitchen\n'
        '  - [ ] sink\n'
        '    - [ ] tap\n'
        '- [ ] hall',
        onToggleTask: toggled.add,
      );

      for (var i = 0; i < 4; i++) {
        await tester.tap(find.byType(Checkbox).at(i));
      }

      expect(toggled, [0, 1, 2, 3]);
    });

    testWidgets('checkboxes are read-only without a toggle callback', (
      tester,
    ) async {
      await _show(tester, '- [ ] milk');

      expect(_checkboxes(tester).single.onChanged, isNull);
    });

    testWidgets('checkboxes follow a callback that is added later', (
      tester,
    ) async {
      final toggled = <int>[];
      await _show(tester, '- [ ] milk');
      await _show(tester, '- [ ] milk', onToggleTask: toggled.add);

      await tester.tap(find.byType(Checkbox));
      expect(toggled, [0]);
    });

    testWidgets('ticking is off when lines and boxes might not line up', (
      tester,
    ) async {
      await _show(
        tester,
        '- [ ] milk\n\n<div>\n- [ ] in html\n</div>',
        onToggleTask: (_) {},
      );

      expect(_checkboxes(tester).single.onChanged, isNull);
    });
  });
}
