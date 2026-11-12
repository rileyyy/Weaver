import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/mention_completion.dart';

void main() {
  const handles = ['claude-research', 'claude-code', 'llama'];

  MentionCompletion? atEnd(String text) =>
      MentionCompletion.at(text, text.length);

  group('MentionCompletion.at', () {
    test('finds the mention being typed at the cursor', () {
      final completion = atEnd('Can you @cla');

      expect(completion?.query, 'cla');
      expect(completion?.start, 8);
    });

    test('a bare @ suggests every handle', () {
      expect(atEnd('@')?.suggest(handles), [
        'claude-code',
        'claude-research',
        'llama',
      ]);
    });

    test('suggests handles starting with what was typed, ignoring case', () {
      expect(atEnd('Hi @Claude-R')?.suggest(handles), ['claude-research']);
    });

    test('a handle typed up to a hyphen still suggests', () {
      expect(atEnd('@claude-')?.suggest(handles), [
        'claude-code',
        'claude-research',
      ]);
    });

    test('nothing is suggested once a space ends the mention', () {
      expect(atEnd('@claude-research '), isNull);
    });

    for (final text in [
      'riley@cla',
      'no mention',
      'e.g.@cla',
      '@@cla',
      '@-cla',
      '@claude--',
    ]) {
      test('"$text" isn\'t a mention being typed', () {
        expect(atEnd(text), isNull);
      });
    }

    test('only the text before the cursor counts', () {
      expect(MentionCompletion.at('@cla and more', 4)?.query, 'cla');
      expect(MentionCompletion.at('@cla and more', 9), isNull);
    });
  });

  test('apply ends the handle with a space, ready for the request', () {
    final completion = atEnd('Hi @cla')!;

    final value = completion.apply(
      const TextEditingValue(
        text: 'Hi @cla',
        selection: TextSelection.collapsed(offset: 7),
      ),
      'claude-research',
    );

    expect(value.text, 'Hi @claude-research ');
    expect(value.selection, const TextSelection.collapsed(offset: 20));
  });

  test('apply mid-text reuses the space already after the mention', () {
    const text = 'Can you @cla find quotes';
    final completion = MentionCompletion.at(text, 12)!;

    final value = completion.apply(
      const TextEditingValue(
        text: text,
        selection: TextSelection.collapsed(offset: 12),
      ),
      'claude-research',
    );

    expect(value.text, 'Can you @claude-research find quotes');
    expect(value.selection, const TextSelection.collapsed(offset: 25));
  });
}
