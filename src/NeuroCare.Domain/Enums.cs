namespace NeuroCare.Domain;

public enum Sex { NotInformed = 0, Female = 1, Male = 2, Other = 3 }
public enum PatientStatus { Active = 1, Inactive = 2 }
public enum AppointmentStatus { Scheduled = 1, Confirmed = 2, Completed = 3, Cancelled = 4, NoShow = 5 }
public enum AppointmentType { FirstVisit = 1, FollowUp = 2, Return = 3, Telemedicine = 4, Exam = 5 }
public enum AppointmentAction { Confirm, Cancel, Complete, NoShow }
