import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';
import 'package:weaver/features/work_item_detail/widgets/attachments_section.dart';

import '../fakes/fake_attachment_repository.dart';
import '../fakes/tiny_png.dart';

Future<void> _pump(
  WidgetTester tester, {
  List<WorkItemAttachment> attachments = const [],
  bool isLoading = false,
  String? loadError,
  bool isUploading = false,
  String? error,
  bool canTakePhoto = false,
  VoidCallback? onUpload,
  VoidCallback? onTakePhoto,
  Future<Uint8List> Function(WorkItemAttachment)? thumbnailOf,
  Future<Uint8List> Function(WorkItemAttachment)? imageOf,
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
        canTakePhoto: canTakePhoto,
        onUpload: onUpload ?? () {},
        onTakePhoto: onTakePhoto ?? () {},
        thumbnailOf: thumbnailOf ?? (_) async => tinyPng,
        imageOf: imageOf ?? (_) async => tinyPng,
        onDownload: onDownload ?? (_) {},
        onDelete: onDelete ?? (_) {},
      ),
    ),
  ),
);

Finder get _attachButton => find.widgetWithText(TextButton, 'Attach file');
Finder get _takePhotoButton => find.widgetWithText(TextButton, 'Take photo');

WorkItemAttachment _photo() => attachment(
  fileName: 'whiteboard.jpg',
  contentType: 'image/jpeg',
  hasThumbnail: true,
);

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

  testWidgets('the download button downloads too', (tester) async {
    final downloaded = <WorkItemAttachment>[];
    final report = attachment();
    await _pump(tester, attachments: [report], onDownload: downloaded.add);

    await tester.tap(find.byTooltip('Download attachment'));

    expect(downloaded, [report]);
  });

  group('preview', () {
    testWidgets('shows the thumbnail of an attachment that has one', (
      tester,
    ) async {
      final requested = <String>[];
      final photo = _photo();
      await _pump(
        tester,
        attachments: [photo],
        thumbnailOf: (a) async {
          requested.add(a.id);
          return tinyPng;
        },
      );
      await tester.pump();

      expect(requested, [photo.id]);
      expect(find.byType(Image), findsOneWidget);
      expect(find.byIcon(Icons.image_outlined), findsNothing);
    });

    testWidgets('shows a placeholder while the thumbnail loads', (
      tester,
    ) async {
      final thumbnail = Completer<Uint8List>();
      await _pump(
        tester,
        attachments: [_photo()],
        thumbnailOf: (_) => thumbnail.future,
      );

      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      expect(find.byType(Image), findsNothing);
    });

    testWidgets('falls back to the image icon when the thumbnail fails', (
      tester,
    ) async {
      await _pump(
        tester,
        attachments: [_photo()],
        thumbnailOf: (_) async => throw Exception('gone'),
      );
      await tester.pump();

      expect(find.byType(Image), findsNothing);
      expect(find.byIcon(Icons.image_outlined), findsOneWidget);
    });

    testWidgets('shows an icon for the file type when there is no '
        'thumbnail, without fetching one', (tester) async {
      final requested = <String>[];
      await _pump(
        tester,
        attachments: [
          attachment(),
          attachment(
            id: 'att-2',
            fileName: 'notes.txt',
            contentType: 'text/plain',
          ),
          attachment(
            id: 'att-3',
            fileName: 'scan.heic',
            contentType: 'image/heic',
          ),
        ],
        thumbnailOf: (a) async {
          requested.add(a.id);
          return tinyPng;
        },
      );

      expect(find.byIcon(Icons.picture_as_pdf_outlined), findsOneWidget);
      expect(find.byIcon(Icons.insert_drive_file_outlined), findsOneWidget);
      expect(find.byIcon(Icons.image_outlined), findsOneWidget);
      expect(find.byType(Image), findsNothing);
      expect(requested, isEmpty);
    });
  });

  testWidgets("an image's download button downloads it", (tester) async {
    final downloaded = <WorkItemAttachment>[];
    final photo = _photo();
    await _pump(tester, attachments: [photo], onDownload: downloaded.add);
    await tester.pump();

    await tester.tap(find.byTooltip('Download attachment'));
    await tester.pump();

    expect(downloaded, [photo]);
    expect(find.byType(InteractiveViewer), findsNothing);
  });

  group('full-size viewer', () {
    testWidgets("tapping an image's row opens it full size, without "
        'downloading it', (tester) async {
      final downloaded = <WorkItemAttachment>[];
      final opened = <String>[];
      final original = Completer<Uint8List>();
      final photo = _photo();
      await _pump(
        tester,
        attachments: [photo],
        onDownload: downloaded.add,
        imageOf: (a) {
          opened.add(a.id);
          return original.future;
        },
      );
      await tester.pump();

      await tester.tap(find.text('whiteboard.jpg'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 300));

      expect(opened, [photo.id]);
      expect(downloaded, isEmpty);
      expect(find.text('whiteboard.jpg'), findsNWidgets(2));
      expect(find.byType(CircularProgressIndicator), findsOneWidget);

      original.complete(tinyPng);
      await tester.pump();

      expect(find.byType(CircularProgressIndicator), findsNothing);
      expect(
        find.descendant(
          of: find.byType(InteractiveViewer),
          matching: find.byType(Image),
        ),
        findsOneWidget,
      );
    });

    testWidgets('says so when the image cannot be loaded', (tester) async {
      await _pump(
        tester,
        attachments: [_photo()],
        imageOf: (_) async => throw Exception('gone'),
      );
      await tester.pump();

      await tester.tap(find.text('whiteboard.jpg'));
      await tester.pumpAndSettle();

      expect(find.text('Could not load whiteboard.jpg.'), findsOneWidget);
      expect(find.byType(InteractiveViewer), findsNothing);
    });

    testWidgets('closes back to the list', (tester) async {
      await _pump(tester, attachments: [_photo()]);
      await tester.pump();
      await tester.tap(find.text('whiteboard.jpg'));
      await tester.pumpAndSettle();

      await tester.tap(find.byTooltip('Close'));
      await tester.pumpAndSettle();

      expect(find.byType(InteractiveViewer), findsNothing);
      expect(find.text('whiteboard.jpg'), findsOneWidget);
    });
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

  testWidgets('offers "Take photo" only where the device has a camera', (
    tester,
  ) async {
    await _pump(tester);
    expect(_takePhotoButton, findsNothing);

    var photos = 0;
    await _pump(tester, canTakePhoto: true, onTakePhoto: () => photos++);
    expect(_attachButton, findsOneWidget);
    await tester.tap(_takePhotoButton);

    expect(photos, 1);
  });

  testWidgets('"Take photo" waits while a file uploads', (tester) async {
    var photos = 0;
    await _pump(
      tester,
      canTakePhoto: true,
      isUploading: true,
      onTakePhoto: () => photos++,
    );

    await tester.tap(_takePhotoButton);

    expect(photos, 0);
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
