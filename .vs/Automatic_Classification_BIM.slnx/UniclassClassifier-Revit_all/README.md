# UniclassClassifier !

Prototype designed to automatically assign Uniclass codes to BIM objects, currently focusing on the higher
levels of the Systems Table. The v0.1 model, now online and in active use, has been trained exclusively on
geometric features such as dimensions, shape descriptors, bounding box metrics, and spatial relationships.
Its purpose is not only to classify unclassified BIM objects but also to detect inconsistencies in existing
classifications.

We are collecting user feedback and storing classification data and inference results to inform future
iterations. Upcoming releases will extend classification to lower-level entries in the Systems Table and to the
Products and Materials Tables, where semantic information (e.g., product descriptions, material properties)
will be incorporated into the training process to improve classification accuracy and granularity.

---

## Authors
- [@matcabfer](https://github.com/matcabfer)
- [@SARAPARECE](https://github.com/SARAPARECE)