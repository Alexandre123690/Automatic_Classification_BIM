#!/usr/bin/env python
# -*- coding: utf-8 -*-

import pandas as pd
import numpy as np
import re
import sys
import joblib
import json
import os

def main():
    # Verifica argumentos
    if len(sys.argv) != 3:
        print("Uso: python classification_script.py <json_file> <model_path>")
        sys.exit(1)

    # Argumentos: JSON de entrada e caminho dos modelos
    json_path = sys.argv[1]
    model_path = sys.argv[2]

    base_dir = os.path.dirname(json_path)
    out_json = os.path.join(base_dir, "classified.json")
    out_csv = os.path.join(base_dir, "classified.csv")

    # Debug para ver os ficheiros recebidos
    print(f"Recebido JSON: {json_path}")
    print(f"Caminho dos modelos: {model_path}")

    # Carregar dados
    df = pd.read_json(json_path)

    # Eliminar colunas não usadas
    drop_cols = [
        'SECClass_Code_EF', 'SECClass_Title_EF', 'SECClasS_Title_Ss','SECClass_Code_Pr', 
        'SECClass_Title_Pr', 'Family and Type', 'Base_constraint', 'Top_constraint', 'Level', 
        'Base_level', 'Top_level', 'Base_offset', 'Top_offset', 'Phase_Created',
        'Start_X', 'Start_Y', 'Start_Z', 'End_X', 'End_Y', 'End_Z', 'Aspect_Ratio', 
        'Volume_to_Surface_Area_Ratio', 'Comments', 'Keynote', 'Description', 'Materials',
        'Category'
    ]
    df.drop(columns=[col for col in drop_cols if col in df.columns], inplace=True)

    # Codificar categorias
    df['ElementID'] = df['ElementID'].astype('category')

    # load_bearing_status
    df['load_bearing_status'] = df['load_bearing_status'].replace("None", "Non Load-Bearing")
    df['load_bearing_status_binary'] = df['load_bearing_status'].astype('category')
    df = pd.get_dummies(df, columns=['load_bearing_status_binary'], drop_first=True)
    if 'load_bearing_status_binary_Non Load-Bearing' not in df.columns:
        df['load_bearing_status_binary_Non Load-Bearing'] = 0
    df['load_bearing_status_binary_Non Load-Bearing'] = df['load_bearing_status_binary_Non Load-Bearing'].astype(float)

    # Variáveis numéricas (força conversão para float, substitui qualquer erro por 0)
    numeric_cols = [
        'Volume', 'Area', 'Length', 'Height', 'Thickness/Width',
        'Bounding_Box_Width', 'Bounding_Box_Height', 'Bounding_Box_Depth',
        'Centroid_X', 'Centroid_Y', 'Centroid_Z',
        'Orientation_Angle', 'Curvature'
    ]

    for col in numeric_cols:
        if col in df.columns:
            df[col] = pd.to_numeric(df[col], errors='coerce').fillna(0.0).astype(float)

    # Converter 'Number_of_Faces' para inteiro
    df['Number_of_Faces'] = pd.to_numeric(df['Number_of_Faces'], errors="coerce").fillna(0).astype(int)

    # Remover colunas temporárias
    cols_to_remove = ['load_bearing_status', 'SECClasS_Code_Ss']
    df.drop(columns=[col for col in cols_to_remove if col in df.columns], inplace=True)

    # Classificação Level 1
    model = joblib.load(os.path.join(model_path, "model_level1.pkl"))
    encoder = joblib.load(os.path.join(model_path, "encodertargetlevel1.pkl"))

    # Antes de prever Level 1
    X_new = df.drop(columns=["ElementID"])

    # Garante que TODAS as colunas existem
    for col in model.feature_names_in_:
        if col not in X_new.columns:
            X_new[col] = 0

    # Ordena as colunas como no treino
    X_new = X_new[model.feature_names_in_]

    # FORÇA todos os valores para numéricos
    X_new = X_new.apply(pd.to_numeric, errors='coerce').fillna(0.0)

    # Previsões Level 1 e probabilidades
    y_pred = model.predict(X_new)
    y_pred_decoded = encoder.inverse_transform(y_pred)

    try:
        y_pred_proba = model.predict_proba(X_new)
        confidences = np.max(y_pred_proba, axis=1)
    except Exception as e:
        print(f"Aviso: não foi possível obter probabilidades com predict_proba ({e}). Confiança assumida como 1.0.")
        confidences = np.ones(len(y_pred), dtype=float)

    # Criar e preencher colunas Ss1
    ss1_columns = ['Ss1_Ss_20', 'Ss1_Ss_25', 'Ss1_Ss_30', 'Ss1_Ss_35', 'Ss1_Ss_37', 'Ss1_Ss_40']
    for col in ss1_columns:
        X_new[col] = 0

    # Preencher colunas Ss1 baseado nas previsões Level 1
    proceed_to_level2 = []
    for idx, (pred, conf) in enumerate(zip(y_pred_decoded, confidences)):
        if conf > 0.70:
            col_name = f'Ss1_Ss_{pred}'
            if col_name in ss1_columns:
                X_new.iloc[idx, X_new.columns.get_loc(col_name)] = 1
            proceed_to_level2.append(idx)

    # Carregar e aplicar modelo XGBoost
    xgb_model = joblib.load(os.path.join(model_path, "model_level2.pkl"))
    encoder_xgb = joblib.load(os.path.join(model_path, "encodertargetlevel2.pkl"))

    # Preparar dados para XGBoost
    if hasattr(xgb_model, 'feature_names_in_'):
        X_new_xgb = X_new[xgb_model.feature_names_in_]
    else:
        X_new_xgb = X_new

    # Previsões XGBoost
    y_pred_xgb = xgb_model.predict(X_new_xgb)
    y_pred_xgb_decoded = encoder_xgb.inverse_transform(y_pred_xgb)

    try:
        y_pred_proba_xgb = xgb_model.predict_proba(X_new_xgb)
        confidences_xgb = np.max(y_pred_proba_xgb, axis=1)
    except Exception as e:
        print(f"Aviso: não foi possível obter probabilidades XGBoost ({e}). Confiança assumida como 1.0.")
        confidences_xgb = np.ones(len(y_pred_xgb), dtype=float)

    # Construir resultados combinados
    results = {}
    for idx, (eid, pred_l1, conf_l1, pred_xgb, conf_xgb) in enumerate(zip(
        df['ElementID'].astype(str), y_pred_decoded, confidences, y_pred_xgb_decoded, confidences_xgb
    )):
        if conf_l1 <= 0.70:
            # Se confiança do Level 1 é baixa, marca como não classificado
            results[str(eid)] = {
                "classification": "unclassified",
                "confidence": float(conf_l1),
                "level": "none"
            }
        else:
            # Se passou no Level 1, verifica Level 2
            if conf_xgb > 0.70:
                # Se Level 2 tem alta confiança, usa classificação Level 2
                results[str(eid)] = {
                    "classification": str(pred_xgb),
                    "confidence": float(conf_xgb),
                    "level": "level_2"
                }
            else:
                # Se Level 2 tem baixa confiança, usa classificação Level 1
                results[str(eid)] = {
                    "classification": str(pred_l1),
                    "confidence": float(conf_l1),
                    "level": "level_1"
                }

    # Estatísticas
    print(f"\nEstatísticas de Classificação:")
    print(f"Total de elementos: {len(df)}")
    print(f"\nResultados:")
    n_unclassified = sum(1 for v in results.values() if v['level'] == 'none')
    n_l1_classified = sum(1 for v in results.values() if v['level'] == 'level_1')
    n_l2_classified = sum(1 for v in results.values() if v['level'] == 'level_2')
    print(f"  - Não classificados (Level 1 ≤70%): {n_unclassified}")
    print(f"  - Classificados por Level 1 (L1 >70%, L2 ≤70%): {n_l1_classified}")
    print(f"  - Classificados por Level 2 (ambos >70%): {n_l2_classified}")

    # Salvar resultados em JSON
    with open(out_json, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)

    # Salvar resultados em CSV
    df_results = pd.DataFrame.from_dict(results, orient='index')
    df_results.index.name = 'ElementID'
    df_results.to_csv(out_csv)

    print(f"\nClassificação concluída com sucesso.")
    print(f"Ficheiros gerados:")
    print(f"- JSON: {out_json}")
    print(f"- CSV: {out_csv}")

if __name__ == "__main__":
    main()