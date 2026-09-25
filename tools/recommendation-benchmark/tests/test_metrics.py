import unittest

from bench.metrics import audience_relation, dcg, evaluate, grade


def label(key, author="A", audience="adulte", form="roman", clusters=("x",), series=None, adaptation_of=None):
    return {"key": key, "author": author, "audience": audience, "form": form,
            "clusters": list(clusters), "series": list(series) if series else None, "adaptation_of": adaptation_of}


class GradeTests(unittest.TestCase):
    def test_same_series_is_best(self):
        self.assertEqual(grade(label("hp1", series=("hp", 1)), label("hp2", series=("hp", 2))), 3)

    def test_same_cluster_same_audience_same_form(self):
        self.assertEqual(grade(label("a"), label("b", author="B")), 2)

    def test_same_cluster_but_youth_versus_adult_is_loose(self):
        self.assertEqual(grade(label("a"), label("b", author="B", audience="jeunesse")), 1)

    def test_adaptation_is_loose(self):
        self.assertEqual(grade(label("1984"), label("1984bd", form="bd", adaptation_of="1984")), 1)

    def test_same_author_without_shared_cluster_is_loose(self):
        self.assertEqual(grade(label("a", clusters=("x",)), label("b", clusters=("y",))), 1)

    def test_unrelated(self):
        self.assertEqual(grade(label("a", clusters=("x",)), label("b", author="B", clusters=("y",))), 0)

    def test_other_edition_of_same_work_is_not_rewarded(self):
        self.assertEqual(grade(label("a"), label("a")), 0)

    def test_tout_public_is_compatible_with_everyone(self):
        self.assertEqual(audience_relation("tout-public", "jeunesse"), "full")
        self.assertEqual(audience_relation("jeunesse", "adulte"), "none")
        self.assertEqual(audience_relation("ado", "adulte"), "partial")


class EvaluateTests(unittest.TestCase):
    def test_perfect_ranking_scores_one_and_detects_leak(self):
        labels = {
            "q#1": label("q", series=("s", 1)),
            "q#2": label("q", series=("s", 1)),
            "n#1": label("n", series=("s", 2)),
            "c#1": label("c", author="B"),
            "z#1": label("z", author="C", clusters=("y",)),
        }
        rankings = {"q#1": [("n#1", 1.0), ("c#1", 0.9), ("q#2", 0.8), ("z#1", 0.1)]}
        score = evaluate("T", rankings, labels)
        self.assertAlmostEqual(score.per_query["q#1"], 1.0)
        # q#1 montre son autre édition (fuite), q#2 n'a aucun voisin : 1 fuite sur 2 requêtes concernées
        self.assertAlmostEqual(score.leak_rate, 0.5)
        self.assertEqual(score.next_tome_hit, 0.5)

    def test_dcg_discounts_by_rank(self):
        self.assertGreater(dcg([3, 0]), dcg([0, 3]))


if __name__ == "__main__":
    unittest.main()
