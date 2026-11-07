import unittest
from service import category, overlap

class ProtocolTests(unittest.TestCase):
    def test_material_does_not_grant_collection(self):
        self.assertEqual(category('TRASH','trash_plastic'),('OtherRubbish',False))
        self.assertEqual(category('YOLOE','plastic bottle'),('CollectableRubbish',True))
    def test_wildlife_remains_hazard(self):
        self.assertEqual(category('TRASH','animal_fish'),('Wildlife',False))
        self.assertEqual(category('YOLOE','fish'),('Wildlife',False))
    def test_contained_wildlife_overlap_is_not_lost(self):
        self.assertEqual(overlap([0,0,100,100],[20,20,30,30]),1)
        self.assertEqual(overlap([0,0,10,10],[20,20,30,30]),0)

if __name__=='__main__':unittest.main()
