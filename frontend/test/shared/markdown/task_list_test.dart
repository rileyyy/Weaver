import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/shared/markdown/task_list.dart';

void main() {
  group('toggleTask', () {
    test('ticks an unticked task', () {
      expect(toggleTask('- [ ] milk', 0), '- [x] milk');
    });

    test('unticks a task ticked with a lower- or upper-case x', () {
      expect(toggleTask('- [x] milk', 0), '- [ ] milk');
      expect(toggleTask('- [X] milk', 0), '- [ ] milk');
    });

    test('toggles only the task it is given', () {
      const list = '- [ ] milk\n- [ ] eggs\n- [ ] bread';
      expect(toggleTask(list, 1), '- [ ] milk\n- [x] eggs\n- [ ] bread');
    });

    test('counts nested tasks in document order', () {
      const list =
          '- [ ] kitchen\n'
          '  - [ ] sink\n'
          '    - [x] tap\n'
          '- [ ] hall';
      expect(
        toggleTask(list, 2),
        '- [ ] kitchen\n'
        '  - [ ] sink\n'
        '    - [ ] tap\n'
        '- [ ] hall',
      );
      expect(toggleTask(list, 3), endsWith('- [x] hall'));
    });

    test('accepts every list marker the renderer does', () {
      const list = '* [ ] a\n+ [ ] b\n1. [ ] c\n2) [ ] d';
      expect(toggleTask(list, 2), '* [ ] a\n+ [ ] b\n1. [x] c\n2) [ ] d');
      expect(toggleTask(list, 3), '* [ ] a\n+ [ ] b\n1. [ ] c\n2) [x] d');
    });

    test('skips task-like lines inside fenced code blocks', () {
      const text =
          '```\n'
          '- [ ] not a task\n'
          '```\n'
          '~~~md\n'
          '- [ ] nor this\n'
          '~~~\n'
          '- [ ] real';
      expect(taskLineNumbers(text), [6]);
      expect(toggleTask(text, 0), endsWith('- [x] real'));
      expect(toggleTask(text, 0), contains('- [ ] not a task'));
    });

    test('a fence only closes with the same character and enough of it', () {
      const text =
          '````\n'
          '```\n'
          '~~~\n'
          '- [ ] still code\n'
          '````\n'
          '- [ ] real';
      expect(taskLineNumbers(text), [5]);
    });

    test('everything after an unclosed fence is code', () {
      expect(taskLineNumbers('- [ ] a\n```\n- [ ] b'), [0]);
    });

    test('skips an indented code block that is not inside a list', () {
      const text = 'Example:\n\n    - [ ] code\n\n- [ ] real';
      expect(taskLineNumbers(text), [4]);
    });

    test('counts tasks in a block quote', () {
      expect(toggleTask('> - [ ] quoted', 0), '> - [x] quoted');
    });

    test('ignores lines that are not tasks', () {
      const text =
          '- [ ]\n' // no text after the box
          '-[ ] no space\n'
          '- [ ]no space\n'
          '- [y] wrong mark\n'
          '[ ] no marker\n'
          '- plain item';
      expect(taskLineNumbers(text), isEmpty);
    });

    test('keeps Windows line endings and surrounding text', () {
      expect(
        toggleTask('Intro\r\n- [ ] a [ ] b\r\nEnd', 0),
        'Intro\r\n- [x] a [ ] b\r\nEnd',
      );
    });

    test('throws for a task index that does not exist', () {
      expect(() => toggleTask('- [ ] a', 1), throwsRangeError);
      expect(() => toggleTask('no tasks', 0), throwsRangeError);
    });
  });

  group('taskLinesMatchRenderer', () {
    test('agrees for ordinary task lists, nested lists and code', () {
      expect(
        taskLinesMatchRenderer(
          '# Shopping\n'
          '- [ ] milk\n'
          '  - [x] semi-skimmed\n'
          '```\n- [ ] code\n```\n'
          '1. [X] eggs',
        ),
        isTrue,
      );
      expect(
        taskLinesMatchRenderer('Example:\n\n    - [ ] code\n\n> - [ ] quoted'),
        isTrue,
      );
      expect(taskLinesMatchRenderer('- [ ]\n- [y] no\n- plain'), isTrue);
    });

    test('disagrees when the renderer would not draw a box the scan found', () {
      // Inside an HTML block, which the renderer shows as plain text.
      expect(taskLinesMatchRenderer('<div>\n- [ ] x\n</div>'), isFalse);
    });
  });
}
