"""自有合成样本验证：身份不随重命名变、裁片脚点保持、选择集不夹带媒体。"""
import importlib.util
import pathlib
import tempfile
import unittest

from PIL import Image

MODULE = pathlib.Path(__file__).with_name('catalog_core.py')


class CatalogContracts(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.api = None
        if MODULE.exists():
            spec = importlib.util.spec_from_file_location('catalog_core', MODULE)
            cls.api = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(cls.api)

    def ready(self):
        self.assertIsNotNone(self.api, 'catalog_core not implemented: no shared identity/selection/canvas contract')
        return self.api

    def test_identity_uses_origin_object_not_display_name_or_content_hash(self):
        api = self.ready()
        self.assertEqual(api.stable_id('7', 'plants_assets_all_abc123.bundle', '-42'),
                         api.stable_id('7', 'plants_assets_all_ffff44.bundle', '-42'))
        self.assertNotEqual(api.stable_id('7', 'plants_assets_all_abc123.bundle', '-42'),
                            api.stable_id('7', 'plants_assets_all_abc123.bundle', '-43'))

    def test_trimmed_image_reconstructs_canvas_without_moving_pixels(self):
        api = self.ready()
        patch = Image.new('RGBA', (2, 3), (5, 80, 30, 255))
        image = api.restore_canvas(patch, 8, 7, 1, 2)
        self.assertEqual(image.size, (8, 7))
        self.assertEqual(image.getpixel((1, 2)), (5, 80, 30, 255))
        self.assertEqual(image.getpixel((0, 2)), (0, 0, 0, 0))
        self.assertEqual(image.getpixel((1, 5)), (0, 0, 0, 0))

    def test_invalid_trim_does_not_silently_clip_source(self):
        api = self.ready()
        with self.assertRaises(ValueError):
            api.restore_canvas(Image.new('RGBA', (3, 4)), 2, 4, 0, 0)

    def test_content_equal_sprites_keep_two_semantic_ids(self):
        api = self.ready()
        rows = [dict(assetId='a', revision='r1'), dict(assetId='b', revision='r1')]
        chosen = api.selection('demo', 'v1', rows, ['b', 'a'])
        self.assertEqual(chosen['entries'], [dict(assetId='b', revision='r1'), dict(assetId='a', revision='r1')])
        self.assertEqual(set(chosen), {'format', 'schemaVersion', 'catalogId', 'version', 'entries'})

    def test_unknown_or_stale_selection_fails_before_import(self):
        api = self.ready()
        with self.assertRaises(ValueError):
            api.selection('demo', 'v1', [dict(assetId='a', revision='r1')], ['missing'])
        with self.assertRaises(ValueError):
            api.selection('demo', 'v1', [dict(assetId='a', revision='r1'), dict(assetId='a', revision='r2')], ['a'])

    def test_media_root_escape_and_missing_path_are_rejected(self):
        api = self.ready()
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            (root / 'safe.txt').write_text('synthetic', encoding='utf-8')
            self.assertEqual(api.resolve_media(root, 'safe.txt').read_text('utf-8'), 'synthetic')
            with self.assertRaises(ValueError):
                api.resolve_media(root, '../secret.txt')
            with self.assertRaises(FileNotFoundError):
                api.resolve_media(root, 'missing.png')

    def test_null_container_ref_keeps_other_metadata_and_records_null(self):
        api = self.ready()
        self.assertTrue(hasattr(api, 'container_rows'), 'null-reference-aware container index not implemented')
        raw = [('ok.asset', {'asset': {'m_FileID': 0, 'm_PathID': 42}}),
               ('missing.asset', {'asset': {'m_FileID': 0, 'm_PathID': 0}})]
        result = api.container_rows(raw, {42: 'Sprite'})
        self.assertEqual(result[0], dict(path='ok.asset', objectId='42', fileId=0, type='Sprite', status='Resolved'))
        self.assertEqual(result[1]['status'], 'NullReference')
        self.assertEqual(result[1]['path'], 'missing.asset')


if __name__ == '__main__':
    unittest.main(verbosity=2)
