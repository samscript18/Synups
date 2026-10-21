import importlib.util,io,stat,tarfile,tempfile,unittest,zipfile
from pathlib import Path
spec=importlib.util.spec_from_file_location('published_smoke',Path(__file__).resolve().parents[1]/'published-smoke.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)

class PublishedArchiveTests(unittest.TestCase):
    def test_zip_traversal_is_rejected_before_writing_outside_output(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);archive=root/'bad.zip'
            with zipfile.ZipFile(archive,'w') as z:z.writestr('../outside','bad')
            with self.assertRaises(ValueError):module.unpack(archive,root/'unpacked')
            self.assertFalse((root/'outside').exists())
    def test_tar_links_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);archive=root/'bad.tar.gz'
            with tarfile.open(archive,'w:gz') as z:
                item=tarfile.TarInfo('Synups/link');item.type=tarfile.SYMTYPE;item.linkname='/tmp/outside';z.addfile(item)
            with self.assertRaises(ValueError):module.unpack(archive,root/'unpacked')
    def test_tar_executable_bytes_and_permissions_survive_extraction(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);archive=root/'good.tar.gz'
            with tarfile.open(archive,'w:gz') as z:
                item=tarfile.TarInfo('Synups/Synups');item.mode=0o755;item.size=4;z.addfile(item,io.BytesIO(b'code'))
            module.unpack(archive,root/'unpacked');executable=root/'unpacked/Synups/Synups'
            self.assertEqual(executable.read_bytes(),b'code')
            if __import__('os').name!='nt':self.assertTrue(executable.stat().st_mode&0o111)

if __name__=='__main__':unittest.main()
