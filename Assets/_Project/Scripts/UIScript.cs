using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIScript : MonoBehaviour
{
    public class ListLoc
    {
        public GameObject go;
        public TMP_Text title;
        public TMP_Text desc;
    }
    public GameObject searchBtn;
    public GameObject searchUI;
    public GameObject searchResult;
    public GameObject routePreview;
    public GameObject startNav;
    public ListLoc[] listLocs;
    public 
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void SearchUI()
    {
        searchUI.SetActive(true);
    }
    public void Search()
    {

    }
    public void ListLocation()
    {

    }
    public void SearchResult()
    {

    }
    public void RoutePreview()
    {

    }
}
