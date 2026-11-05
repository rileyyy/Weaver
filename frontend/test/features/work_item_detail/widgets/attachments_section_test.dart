import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/attachments_section.dart';

import '../fakes/fake_attachment_repository.dart';

Future<void> _pump(
  WidgetTester tester, {
  List<WorkItemAttachment> attachments = const [],
  bool isLoading = false,
  String? loadError,
  bool isUploading = false,
  String? error,
  VoidCallback? onUpload,
  ValueChanged<WorkItemAttachment>? onDownload,
  ValueChanged<WorkItemAttachment>? onDelete,
}) => tester.pumpWidget(
  MaterialApp(
    home: Scaffold(
      body: AttachmentsSection(
        attachments: attachments,
        isLoading: isLoading,
        loadError: loadError,
        isUploading: isUploading,
        error: error,
        onUpload: onUpload ?? () {},
        onDownload: onDownload ?? (_) {},
        onDelete: onDelete ?? (_) {},
      ),
    ),
  ),
);

Finder get _attachButton => find.widgetWithText(TextButton, 'Attach file');

void main() {
  testWidgets('lists each attachment with its size', (tester) async {
    await _pump(
      tester,
      attachments: [
        attachment(sizeBytes: 1536),
        attachment(id: 'att-2', fileName: 'notes.txt', sizeBytes: 12),
      ],
    );

    expect(find.text('Attachments'), findsOneWidget);
    expect(find.text('report.pdf'), findsOneWidget);
    expect(find.text('1.5 KB'), findsOneWidget);
    expect(find.text('notes.txt'), findsOneWidget);
    expect(find.text('12 B'), findsOneWidget);
  });

  testWidgets('tapping an attachment downloads it', (tester) async {
    final downloaded = <WorkItemAttachment>[];
    final report = attachment();
    await _pump(tester, attachments: [report], onDownload: downloaded.add);

    await tester.tap(find.text('report.pdf'));

    expect(downloaded, [report]);
  });

  testWidgets('deleting asks first, naming the file', (tester) async {
    final deleted = <WorkItemAttachment>[];
    final report = attachment();
    await _pump(tester, attachments: [report], onDelete: deleted.add);

    await tester.tap(find.byTooltip('Delete attachment'));
    await tester.pumpAndSettle();

    expect(find.text('Delete attachment?'), findsOneWidget);
    expect(find.textContaining('report.pdf will be deleted'), findsOneWidget);
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
    expect(deleted, isEmpty);

    await tester.tap(find.byTooltip('Delete attachment'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
    await tester.pumpAndSettle();
    expect(deleted, [report]);
  });

  testWidgets('the attach button uploads', (tester) async {
    var uploads = 0;
    await _pump(tester, onUpload: () => uploads++);

    await tester.tap(_attachButton);

    expect(uploads, 1);
  });

  testWidgets('the attach button waits while a file uploads', (tester) async {
    var uploads = 0;
    await _pump(tester, isUploading: true, onUpload: () => uploads++);

    expect(_attachButton, findsNothing);
    await tester.tap(find.widgetWithText(TextButton, 'Uploading…'));

    expect(uploads, 0);
  });

  testWidgets('shows why the last action failed', (tester) async {
    await _pump(tester, error: 'Attachments can be at most 25 MB.');

    expect(find.text('Attachments can be at most 25 MB.'), findsOneWidget);
  });

  testWidgets('shows a load failure instead of the list', (tester) async {
    await _pump(
      tester,
      attachments: [attachment()],
      loadError: 'Could not load the attachments.',
    );

    expect(find.text('Could not load the attachments.'), findsOneWidget);
    expect(find.text('report.pdf'), findsNothing);
    expect(_attachButton, findsNothing);
  });

  testWidgets('shows progress while the list loads', (tester) async {
    await _pump(tester, isLoading: true);

    expect(find.byType(LinearProgressIndicator), findsOneWidget);
  });
}
