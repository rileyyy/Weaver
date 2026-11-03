import 'package:markdown/markdown.dart' as md;

/// The Markdown dialect Weaver renders. The task-line scan below follows
/// its rules, so the renderer must use the same set.
final md.ExtensionSet markdownExtensions = md.ExtensionSet.gitHubFlavored;

final RegExp _quotePrefix = RegExp(r'^(?:[ ]{0,3}>[ \t]?)*');
final RegExp _fence = RegExp(r'^[ \t]*(`{3,}|~{3,})');
final RegExp _listItem = RegExp(r'^[ \t]*(?:[*+-]|\d{1,9}[.)])(?:[ \t]|$)');

// The renderer only draws a box when whitespace follows `]`, so `- [ ]`
// on its own stays a plain list item.
final RegExp _taskBox = RegExp(
  r'^([ \t]*(?:[*+-]|\d{1,9}[.)])[ \t]+)\[([ xX])\](?=[ \t])',
);

/// The zero-based line numbers of [markdown]'s task lines (`- [ ] …`,
/// `1. [x] …`), in the order the renderer draws their checkboxes. Lines in
/// fenced or indented code blocks don't count.
List<int> taskLineNumbers(String markdown) {
  final lines = markdown.split('\n');
  final tasks = <int>[];
  String? openFence;
  var inList = false;
  var previousBlank = true;

  for (var i = 0; i < lines.length; i++) {
    final body = _stripQuote(lines[i]);
    final fence = _fence.firstMatch(body)?.group(1);

    if (openFence != null) {
      if (_closes(fence, body, openFence)) openFence = null;
      continue;
    }
    if (fence != null) {
      openFence = fence;
      continue;
    }

    final isBlank = body.trim().isEmpty;
    final indent = _indentWidth(body);
    final isListItem = _listItem.hasMatch(body);
    // Without a list to belong to, four columns of indent is a code block
    // (or a paragraph's continuation), never a list item.
    final canBeListItem = isListItem && (indent < 4 || inList);

    if (canBeListItem) {
      inList = true;
      if (_taskBox.hasMatch(body)) tasks.add(i);
    } else if (!isBlank && indent == 0 && previousBlank) {
      inList = false;
    }
    previousBlank = isBlank;
  }
  return tasks;
}

/// [markdown] with the [taskIndex]th task line (counted as in
/// [taskLineNumbers]) ticked if it was unticked and unticked otherwise.
/// Nothing else in the text changes. Throws a [RangeError] if there is no
/// such task.
String toggleTask(String markdown, int taskIndex) {
  final taskLines = taskLineNumbers(markdown);
  RangeError.checkValidIndex(taskIndex, taskLines, 'taskIndex');
  final lines = markdown.split('\n');
  final lineNumber = taskLines[taskIndex];
  final line = lines[lineNumber];
  final quote = _quotePrefix.firstMatch(line)!.group(0)!;
  final toggled = line
      .substring(quote.length)
      .replaceFirstMapped(
        _taskBox,
        (match) => '${match[1]}[${match[2] == ' ' ? 'x' : ' '}]',
      );
  lines[lineNumber] = '$quote$toggled';
  return lines.join('\n');
}

/// Whether the renderer draws exactly as many checkboxes as
/// [taskLineNumbers] finds. Ticking maps the nth checkbox to the nth task
/// line, so where they disagree (e.g. a task line inside an HTML block),
/// ticking would change the wrong line and must be turned off.
bool taskLinesMatchRenderer(String markdown) {
  final nodes = md.Document(
    extensionSet: markdownExtensions,
    encodeHtml: false,
  ).parse(markdown);
  return _countCheckboxes(nodes) == taskLineNumbers(markdown).length;
}

int _countCheckboxes(List<md.Node>? nodes) {
  var count = 0;
  for (final node in nodes ?? const <md.Node>[]) {
    if (node is! md.Element) continue;
    if (node.tag == 'input' && node.attributes['type'] == 'checkbox') count++;
    count += _countCheckboxes(node.children);
  }
  return count;
}

String _stripQuote(String line) =>
    line.substring(_quotePrefix.firstMatch(line)!.group(0)!.length);

bool _closes(String? fence, String body, String openFence) =>
    fence != null &&
    fence[0] == openFence[0] &&
    fence.length >= openFence.length &&
    body.trim() == fence;

int _indentWidth(String line) {
  var width = 0;
  for (final char in line.split('')) {
    if (char == ' ') {
      width++;
    } else if (char == '\t') {
      width += 4 - width % 4;
    } else {
      break;
    }
  }
  return width;
}
